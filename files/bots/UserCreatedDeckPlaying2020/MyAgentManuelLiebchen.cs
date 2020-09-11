using System;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using System.Linq;
using System.Diagnostics;
using SabberStoneCore.Model.Entities;
using SabberStoneBasicAI.Meta;
using SabberStoneCore.Enums;

// TODO choose your own namespace by setting up <submission_tag>
// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.manuelliebchen
{
	class MyScoreManuel : Score.Score
	{
		public override int Rate()
		{
			if(OpHeroHp < 1)
			{
				return int.MaxValue;
			}
			if(HeroHp < 1)
			{
				return int.MinValue;
			}

			float score = 0;
			score += HeroHp - OpHeroHp;
			score += BoardZone.Count() - OpBoardZone.Count();
			score += HeroAtk - OpHeroAtk;

			foreach (Minion Minion in BoardZone)
			{
				score += MinionScore(Minion, true);
			}

			foreach (Minion Minion in OpBoardZone)
			{
				score -= MinionScore(Minion, false);
			}

			return (int) Math.Round(score);
		}

		private float MinionScore(Minion Minion, bool friendly)
		{
			float MinionValue = 1;
			if (Minion.HasTaunt)
			{
				MinionValue += 1;
			}
			if (Minion.IsSilenced)
			{
				MinionValue -= 0.5f;
			}
			return (Minion.Health + Minion.AttackDamage) * MinionValue;
		}

		public override Func<List<IPlayable>, List<int>> MulliganRule()
		{
			return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
		}
	}
	class MyAgentManuelLiebchen : AbstractAgent
	{
		static Random rnd = new Random();
		Stopwatch sw = new Stopwatch();

		int thinkcounter = 0;
		int playerid = 0;

		public MyAgentManuelLiebchen()
		{
			preferedDeck = Decks.MidrangeJadeShaman; //defualt value if no deck is provided
			preferedHero = CardClass.SHAMAN; //default value
		}

		public override PlayerTask GetMove(POGame game)
		{
			playerid = game.CurrentPlayer.PlayerId;
			thinkcounter++;
			sw.Start();

			float timeLeft = 1 - sw.ElapsedMilliseconds / 30000.0f;
			List<PlayerTask> options = game.CurrentPlayer.Options();
	
			Dictionary<PlayerTask, int> values = new Dictionary<PlayerTask, int>();
			int depth = 2;
			if (sw.ElapsedMilliseconds > 15000)
			{
				depth--;
			}
			if(sw.ElapsedMilliseconds > 25000)
			{
				depth--;
			}
			values = SimulateAll(game, options, depth);

			PlayerTask besttask = values.OrderBy(x => x.Value).ToList().Last().Key;
			game.Process(besttask);
			if (Score(game) == int.MaxValue)
			{
				Console.WriteLine("Won after: " + Math.Round(sw.ElapsedMilliseconds / 1000.0f) + "s; Turns: " + thinkcounter);
			}
			sw.Stop();
			return besttask;
		}

		private Dictionary<PlayerTask, int> SimulateAll( POGame initgame, List<PlayerTask> opitons, int depth)
		{
			Dictionary<PlayerTask, POGame> sims = initgame.Simulate(opitons);
			Dictionary<PlayerTask, int> scores = new Dictionary<PlayerTask, int>();
			foreach (KeyValuePair<PlayerTask, POGame> sim in sims) {
				int score = int.MinValue;
				if(sim.Key.PlayerTaskType == PlayerTaskType.END_TURN || depth <= 0)
				{
					score = Score(sim.Value);
				} else
				{
					List<PlayerTask> new_options = sim.Value.CurrentPlayer.Options();
					if (new_options.Count() > 0) {
						score = SimulateAll(sim.Value, new_options, depth - 1).ToList().Max(x => x.Value);
					} else
					{
						score = Score(sim.Value);
					}
				}
				scores.Add(sim.Key, score);
			}
			return scores;
		}

		// Calculate different scores based on our hero's class
		private int Score(POGame state)
		{
			return new MyScoreManuel { Controller = ( playerid == state.CurrentPlayer.PlayerId ? state.CurrentPlayer : state.CurrentOpponent) }.Rate();
		}

		public override void InitializeAgent()
		{
		}

		public override void InitializeGame()
		{
		}

		public override void FinalizeAgent()
		{
		}

		public override void FinalizeGame()
		{
		}
	}
}
