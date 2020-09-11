using SabberStoneBasicAI.Meta;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;


// TODO choose your own namespace by setting up <submission_tag>
// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.ThreeTypeDynLooker
{
	class ThreeTypeDynLooker : AbstractAgent
	{
		

		internal static double hWarrior = 0.0;
		internal static double hMage = 0.0;
		internal static double hShaman = 0.0;

		public ThreeTypeDynLooker()
		{
			preferedDeck = Decks.MidrangeJadeShaman; //defualt value if no deck is provided
			preferedHero = CardClass.SHAMAN; //default value
		}

		public override void InitializeAgent() { }
		public override void FinalizeAgent() { }
		public override void InitializeGame() { }
		public override void FinalizeGame() { }
		public override PlayerTask GetMove(POGame poGame)
		{
			//Console.WriteLine("NEW TURN:");

			Controller player = poGame.CurrentPlayer;

			if (player.HeroClass == CardClass.WARRIOR)
			{
				hWarrior = 1; hMage = 0; hShaman = 0;
			}
			else if (player.HeroClass == CardClass.MAGE)
			{
				hWarrior = 0; hMage = 1; hShaman = 0;
			}
			else
			{
				hWarrior = 0; hMage = 0; hShaman = 1;
			}

			if (player.MulliganState == Mulligan.INPUT)
			{
				List<int> mulligan = new MyScore().GetMulliganRule().Invoke(player.Choice.Choices.Select(p => poGame.getGame().IdEntityDic[p]).ToList());
			}

			//  Get all simulation results for simulations that didn't fail
			IEnumerable<KeyValuePair<PlayerTask, POGame>> validOpts = poGame.Simulate(player.Options()).Where(x => x.Value != null);

			if (validOpts.Any())
			{
				var move = new KeyValuePair<PlayerTask, int>(validOpts.First().Key, Score(poGame, player.PlayerId));
				var nScore = new KeyValuePair<PlayerTask, int>(validOpts.First().Key, Score(poGame, player.PlayerId));
				foreach (KeyValuePair<PlayerTask, POGame> playerTask in validOpts)
				{
					if (validOpts.Count() >= 8)
					{
						if (validOpts.Count() >= 24)
						{
							nScore = nextScore(playerTask, player.PlayerId, 1);
							if (nScore.Value > move.Value)
							{
								move = nScore;
							}
						}
						else
						{
							nScore = nextScore(playerTask, player.PlayerId, 2);
							if (nScore.Value > move.Value)
							{
								move = nScore;
							}
						}
					}
					else
					{
						nScore = nextScore(playerTask, player.PlayerId, 3);
						if (nScore.Value > move.Value)
						{
							move = nScore;
						}
					}
				}//Console.WriteLine(poGame.PartialPrint());
				 //Console.WriteLine("......................");
				 //Console.WriteLine(move.Key +" mit: "+ move.Value);
				 //Console.WriteLine("-----------------------");
				return move.Key;
			}
			else
			{
				return player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);
			}
		}

		public KeyValuePair<PlayerTask, int> nextScore(KeyValuePair<PlayerTask, POGame> task, int playerid, int depth)
		{
			int max_score = Int32.MinValue;
			if (depth > 0 && task.Value.CurrentPlayer.PlayerId == playerid)
			{
				IEnumerable<KeyValuePair<PlayerTask, POGame>> validOpts = task.Value.Simulate(task.Value.CurrentPlayer.Options()).Where(x => x.Value != null);
				if (validOpts.Any())
				{
					foreach (KeyValuePair<PlayerTask, POGame> option in validOpts)
					{
						max_score = Math.Max(max_score, nextScore(option, playerid, depth - 1).Value);
					}
				}
			}
			max_score = Math.Max(max_score, Score(task.Value, playerid));
			//Console.WriteLine(task.Key.ToString() + " mit: " + max_score + " ---  " +depth);
			return new KeyValuePair<PlayerTask, int>(task.Key, max_score);
		}
		internal int Score(POGame state, int playerId)
		{
			Controller p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;
			return new MyScore { Controller = p }.Rate();
		}
	}
	class MyScore : Score.Score
	{
		public override int Rate()
		{
			if (HeroHp < 1)
				return Int32.MinValue;

			if (OpHeroHp < 1)
				return Int32.MaxValue;

			double turn = Controller.Game.Turn / 2;
			double result = 0;

			if (OpBoardZone.Count == 0 && BoardZone.Count > 0)
				result += 5000 + Math.Pow(turn, 2) * 50;

			result += BoardZone.Count * (30 * ThreeTypeDynLooker.hWarrior + 40 + ThreeTypeDynLooker.hMage + 50 * ThreeTypeDynLooker.hShaman);
			result -= OpBoardZone.Count * (25 * ThreeTypeDynLooker.hWarrior + 20 + ThreeTypeDynLooker.hMage + 40 * ThreeTypeDynLooker.hShaman);

			if (OpMinionTotHealthTaunt > 0)
				result += MinionTotHealthTaunt * (-1000 * ThreeTypeDynLooker.hWarrior + -25 * ThreeTypeDynLooker.hMage + -500 * ThreeTypeDynLooker.hShaman);

			result += Math.Sqrt(HeroHp * (100 + ThreeTypeDynLooker.hWarrior + 20 * ThreeTypeDynLooker.hMage + 20 * ThreeTypeDynLooker.hShaman));
			result -= Math.Sqrt(OpHeroHp * (120 + ThreeTypeDynLooker.hWarrior +30 * ThreeTypeDynLooker.hMage + 22 * ThreeTypeDynLooker.hShaman));

			result += MinionTotHealth * (40 * ThreeTypeDynLooker.hWarrior + 50 * ThreeTypeDynLooker.hMage + 10 * ThreeTypeDynLooker.hShaman);
			result -= OpMinionTotHealth * (60 * ThreeTypeDynLooker.hWarrior + 90 * ThreeTypeDynLooker.hMage + 12 * ThreeTypeDynLooker.hShaman);

			result += MinionTotAtk * (80 * ThreeTypeDynLooker.hWarrior + 60 * ThreeTypeDynLooker.hMage + 20 * ThreeTypeDynLooker.hShaman);
			result -= OpMinionTotAtk * (100 * ThreeTypeDynLooker.hWarrior + 85 * ThreeTypeDynLooker.hMage + 25 * ThreeTypeDynLooker.hShaman);

			return (int)Math.Round(result);
		}
		public Func<List<IPlayable>, List<int>> GetMulliganRule()
		{
			Func<List<IPlayable>, List<int>> mulliganScore = p => p.Where(t => (t.Cost > 3)).Select(t => t.Id).ToList();
			return mulliganScore;
		}
	}
}









