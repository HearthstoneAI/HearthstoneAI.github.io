using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Enums;
using SabberStoneBasicAI.Meta;

namespace SabberStoneBasicAI.AIAgents.JoachimKnobi
{
	class JoachimKnobi : AbstractAgent
	{
		public int[] Values = null;

		const int maxSimPerStep = 10;
		const bool useThirdStep = true;

		public JoachimKnobi()
		{
			preferedDeck = Decks.MidrangeJadeShaman; //defualt value if no deck is provided
			preferedHero = CardClass.SHAMAN; //default value
		}

		public override void InitializeAgent() { }
		public override void InitializeGame() { }
		public override void FinalizeGame() { }
		public override void FinalizeAgent() { }


		public override PlayerTask GetMove(POGame game)
		{
			var player = game.CurrentPlayer;
			int pid = player.PlayerId;
			if (player.MulliganState == Mulligan.INPUT)
			{
				List<int> mulligan = new KnobiScore().MulliganRule().Invoke(player.Choice.Choices.Select(p => game.getGame().IdEntityDic[p]).ToList());
				return ChooseTask.Mulligan(player, mulligan);
			}

			// simulation stage 1
			var validOpts = game.Simulate(player.Options()).Where(x => x.Value != null);
			if (validOpts.Any())
			{
				PlayerTask task = validOpts.First().Key;
				// initialize best score as current score
				int bestScore = Score(game, pid);

				// simulate the (maxSimPerStep) best solutions
				int i1 = 0;
				foreach (var opt in validOpts.OrderBy(x => -Score(x.Value, pid)))
				{

					if (i1 > maxSimPerStep || opt.Key.PlayerTaskType == PlayerTaskType.END_TURN) continue;
					++i1;
					int optScore = Score(opt.Value, pid);
					// return action if it leads to win
					if (optScore == Int32.MaxValue) return opt.Key;
					// skip action if it leads to defete
					if (optScore == Int32.MinValue) continue;

					// start simulation stage 2
					var validOpts2 = opt.Value.Simulate(opt.Value.CurrentPlayer.Options()).Where(x => x.Value != null);
					int i2 = 0;
					foreach (var opt2 in validOpts2.OrderBy(x => -Score(x.Value, pid)))
					{
						if (i2 > maxSimPerStep || opt2.Key.PlayerTaskType == PlayerTaskType.END_TURN) continue;
						++i2;
						optScore = Math.Max(optScore, Score(opt2.Value, pid));
						if (optScore == Int32.MaxValue) return opt.Key;
						if (optScore == Int32.MinValue) continue;

						//start simulation stage 3
						if (useThirdStep)
						{
							var validOpts3 = opt.Value.Simulate(opt.Value.CurrentPlayer.Options()).Where(x => x.Value != null);
							foreach (var opt3 in validOpts3.OrderBy(x => -Score(x.Value, pid)))
							{
								if (opt3.Key.PlayerTaskType == PlayerTaskType.END_TURN)
									continue;
								optScore = Math.Max(optScore, Score(opt3.Value, pid));
								if (optScore == Int32.MaxValue) return opt.Key;
								if (optScore == Int32.MinValue) continue;
							}
						}
					}
					// update desicion, if it is better
					if (optScore > bestScore)
					{
						bestScore = optScore;
						task = opt.Key;
					}
				}
				return task;
			}
			// if there are no possible solutions, end the turn
			return player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);
		}

		private int Score(POGame state, int playerId)
		{
			Controller p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;
			// 82%
			if (Values == null)
				return new KnobiScore { Controller = p }.Rate();
			return new KnobiScore { Controller = p, Values = Values }.Rate();
		}
	}

	public class KnobiScore : Score.Score
	{
		public int[] Values = new int[]
		{
			22,
			34,
			41,
			19,
			54,
			61,
			89,
			85,
			5,
			5
		};

		public override int Rate()
		{
			// Final actions
			if (OpHeroHp < 1)
				return Int32.MaxValue;
			if (HeroHp < 1)
				return Int32.MinValue;
			int result = 0;

			result += HeroHp * Values[0];
			result -= OpHeroHp * Values[1];
			result += BoardZone.Count() * Values[2];
			result -= OpBoardZone.Count() * Values[3];
			result += MinionTotAtk * Values[4];
			result -= OpMinionTotAtk * Values[5];
			result += MinionTotHealth * Values[6];
			result -= OpMinionTotHealth * Values[7];
			result += MinionTotHealthTaunt * Values[8];
			result -= OpMinionTotHealthTaunt * Values[9];

			return result;
		}

		public override Func<List<IPlayable>, List<int>> MulliganRule()
		{
			return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
		}
	}
}
