#define DEBUG
#define RELEASE
using System;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model.Entities;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneBasicAI.Meta;

/* Based on DynamicLookaheadAgent.
 * 
 * Contributors:
 * - Nils Bohnhof
 * - Jon-Mailes Graeffe
 * a.k.a. the CopyCats
 */


namespace SabberStoneBasicAI.AIAgents.CopyCats
{
	
	class LynamicDookaheadAgentV1Bachelor : AbstractAgent
	{
		public LynamicDookaheadAgentV1Bachelor()
		{
			preferedDeck = Decks.MidrangeJadeShaman; //defualt value if no deck is provided
			preferedHero = CardClass.SHAMAN; //default value
		}

		class WarriorSubAgent : AbstractAgent
		{
			class CustomScore : Score.Score
			{
				public int HeroArmor => Controller.Hero.Armor;
				public int OpArmor => Controller.Opponent.Hero.Armor;

				public static double[] BaseScaling = new double[] {
					21.5, 33.6, 41.1, 19.4,	54, 60.5, 88.5,	84.7
				};

				public int Rate(double[] runtimeScaling)
				{
					if (OpHeroHp < 1)
						return Int32.MaxValue;

					if (HeroHp < 1)
						return Int32.MinValue;

					double score = 0.0;

					score += BaseScaling[0] * runtimeScaling[0] * (HeroHp+HeroArmor);
					score -= BaseScaling[1] * runtimeScaling[1] * (OpHeroHp+ OpArmor);
					score += 0.01 * HeroAtk;
					score += BaseScaling[2] * runtimeScaling[2] * BoardZone.Count;
					score -= BaseScaling[3] * runtimeScaling[3] * OpBoardZone.Count;

					score += BaseScaling[4] * runtimeScaling[4] * MinionTotHealth;
					score += BaseScaling[5] * runtimeScaling[5] * MinionTotAtk;

					score -= BaseScaling[6] * runtimeScaling[6] * OpMinionTotHealth;
					score -= BaseScaling[7] * runtimeScaling[7] * OpMinionTotAtk;

					return (int)Math.Round(score);
				}

				public override Func<List<IPlayable>, List<int>> MulliganRule()
				{
					return p => p.Where(t => t.Cost > 2).Select(t => t.Id).ToList();
				}
			}
			/* Cannot be used when using threads
			public void PrintLog(POGame game, IOrderedEnumerable<KeyValuePair<PlayerTask, int>> scoreres)
			{
				var player = game.CurrentPlayer;
				var validOpts = game.Simulate(player.Options()).Where(x => x.Value != null);
				var score_res = scoreres;
				Console.WriteLine("Round Nr:" + Convert.ToString(game.Turn));
				Console.WriteLine("HeroHP: " + Convert.ToString(player.Hero.Health) + "\tOppHP: " + Convert.ToString(game.CurrentOpponent.Hero.Health));
				Console.WriteLine("HeroMinionHP: " + Convert.ToString(player.BoardZone.Sum(p => p.Health)) + "\tOppMinionHP: " + Convert.ToString(game.CurrentOpponent.BoardZone.Sum(p => p.Health)));
				Console.WriteLine("HeroMinionAtk: " + Convert.ToString(player.BoardZone.Sum(p => p.AttackDamage)) + "\tOppMinionAtk: " + Convert.ToString(game.CurrentOpponent.BoardZone.Sum(p => p.AttackDamage)));
				foreach (var tmp_score in score_res)
				{

					Console.WriteLine(Convert.ToString(tmp_score.Key) + Convert.ToString(tmp_score.Value));
				}
				Console.WriteLine("-------------------------------------------------------");
			}
			*/
			public override PlayerTask GetMove(POGame game)
			{
				var player = game.CurrentPlayer;

				// Implement a simple Mulligan Rule
				if (player.MulliganState == Mulligan.INPUT)
				{
					List<int> mulligan = new CustomScore().MulliganRule().Invoke(player.Choice.Choices.Select(p => game.getGame().IdEntityDic[p]).ToList());
					return ChooseTask.Mulligan(player, mulligan);
				}

				var opponent = game.CurrentOpponent;
				var options = player.Options();
				var validOpts = game.Simulate(options).Where(x => x.Value != null);
				var optcount = validOpts.Count();



				
				if (game.Turn == 1)
				{
					var opt1 = options.Where(x => x.HasSource && x.Source.Card.Name == "N'Zoth's First Mate");
					if (opt1.Count() > 0)
					{
						

						/*
						Console.WriteLine(opt1);
						Console.WriteLine(opt1.First());
						Console.WriteLine(opt1.Last());
						*/
						return opt1.First();
					}
				}
				
				
				if(game.Turn==3)
				{
						var opt2 = options.Where(x => x.HasSource && x.Source.Card.Name == "Fiery War Axe");
						if (opt2.Count() > 0)
							return opt2.First();
				}
				if (game.Turn == 5)
				{
					var opt2 = options.Where(x => x.HasSource && x.Source.Card.Name == "Arcanite Reaper");
					if (opt2.Count() > 0)
						return opt2.First();
				}
				

				/*
				if (player.Hero.Health < DEFENSE_HEALTH_THRESHOLD * player.Hero.BaseHealth)
					RuntimeScaling[0] += 0.1;

				if (opponent.Hero.Health < DEFENSE_HEALTH_THRESHOLD * opponent.Hero.Health)
					RuntimeScaling[1] += 0.1;
				*/
				var returnValue = validOpts.Any() ?
					validOpts.Select(x => score(x, player.PlayerId, (optcount >= 5) ? ((optcount >= 25) ? 1 : 2) : 3)).OrderBy(x => x.Value).Last().Key :
					player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);

				return returnValue;

				KeyValuePair<PlayerTask, int> score(KeyValuePair<PlayerTask, POGame> state, int player_id, int max_depth = 3)
				{
					int max_score = int.MinValue;
					if (max_depth > 0 && state.Value.CurrentPlayer.PlayerId == player_id)
					{
						var subactions = state.Value.Simulate(state.Value.CurrentPlayer.Options()).Where(x => x.Value != null);

						foreach (var subaction in subactions)
							max_score = Math.Max(max_score, score(subaction, player_id, max_depth - 1).Value);


					}
					max_score = Math.Max(max_score, Score(state.Value, player_id));
					return new KeyValuePair<PlayerTask, int>(state.Key, max_score);
				}
			}

			private int Score(POGame state, int playerId)
			{
				var p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;
				return new CustomScore { Controller = p }.Rate(RuntimeScaling);
			}

			public override void FinalizeAgent() {}
			public override void FinalizeGame() {}
			public override void InitializeAgent() {}
			public override void InitializeGame() {}
		}


		class MageSubAgent : AbstractAgent
		{
			class CustomScore : Score.Score
			{
				public int HeroArmor => Controller.Hero.Armor;
				public int OpArmor => Controller.Opponent.Hero.Armor;

				public static double[] BaseScaling = new double[] {
					21.5, 33.6, 41.1, 19.4, 54, 60.5, 88.5, 84.7
				};

				public int Rate(double[] runtimeScaling)
				{
					if (OpHeroHp < 1)
						return Int32.MaxValue;

					if (HeroHp < 1)
						return Int32.MinValue;

					double score = 0.0;

					score += BaseScaling[0] * runtimeScaling[0] * (HeroHp + HeroArmor);
					score -= BaseScaling[1] * runtimeScaling[1] * (OpHeroHp + OpArmor);
					score += 0.01 * HeroAtk;
					score += BaseScaling[2] * runtimeScaling[2] * BoardZone.Count;
					score -= BaseScaling[3] * runtimeScaling[3] * OpBoardZone.Count;

					score += BaseScaling[4] * runtimeScaling[4] * MinionTotHealth;
					score += BaseScaling[5] * runtimeScaling[5] * MinionTotAtk;

					score -= BaseScaling[6] * runtimeScaling[6] * OpMinionTotHealth;
					score -= BaseScaling[7] * runtimeScaling[7] * OpMinionTotAtk;

					return (int)Math.Round(score);
				}

				public override Func<List<IPlayable>, List<int>> MulliganRule()
				{
					//return p => p.Where(t => t.Card.Name != "Reno Jackson" && t.Cost > 3).Select(t => t.Id).ToList();
					return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
				}
			}

			public override PlayerTask GetMove(POGame game)
			{
				var player = game.CurrentPlayer;

				// Implement a simple Mulligan Rule
				if (player.MulliganState == Mulligan.INPUT)
				{
					List<int> mulligan = new CustomScore().MulliganRule().Invoke(player.Choice.Choices.Select(p => game.getGame().IdEntityDic[p]).ToList());
					return ChooseTask.Mulligan(player, mulligan);
				}

				var opponent = game.CurrentOpponent;
				var options = player.Options();
				
				

				var validOpts = game.Simulate(options).Where(x => x.Value != null);
				var optcount = validOpts.Count();
#if DEBUG
				/*
				var score_res = validOpts.Select(x => score(x, player.PlayerId, (optcount >= 5) ? ((optcount >= 25) ? 1 : 2) : 3)).OrderBy(x => x.Value);

				Console.WriteLine("Round Nr:" + Convert.ToString(game.Turn));
				Console.WriteLine("HeroHP: " + Convert.ToString(player.Hero.Health) + "\tOppHP: " + Convert.ToString(game.CurrentOpponent.Hero.Health));
				Console.WriteLine("HeroMinionHP: " + Convert.ToString(player.BoardZone.Sum(p => p.Health)) + "\tOppMinionHP: " + Convert.ToString(game.CurrentOpponent.BoardZone.Sum(p => p.Health)));
				Console.WriteLine("HeroMinionAtk: " + Convert.ToString(player.BoardZone.Sum(p => p.AttackDamage)) + "\tOppMinionAtk: " + Convert.ToString(game.CurrentOpponent.BoardZone.Sum(p => p.AttackDamage)));
				Console.WriteLine("HeroNbMinions: " + Convert.ToString(player.BoardZone.Count) + "\tOppMinionNB: " + Convert.ToString(game.CurrentOpponent.BoardZone.Count));
				
				
				foreach (var tmp_score in score_res)
					{

						Console.WriteLine(Convert.ToString(tmp_score.Key) + Convert.ToString(tmp_score.Value));
					}
					Console.WriteLine("-------------------------------------------------------");
				//PrintLog(game,  score_res);
				*/
#endif


				/*
				if (player.Hero.Health < DEFENSE_HEALTH_THRESHOLD * player.Hero.BaseHealth)
					RuntimeScaling[0] += 0.1;

				if (opponent.Hero.Health < DEFENSE_HEALTH_THRESHOLD * opponent.Hero.Health)
					RuntimeScaling[1] += 0.1;
				*/
				var opt1 = options.Where(x => x.HasSource && x.Source.Card.Name == "Reno Jackson");
				//if (opt1.Count() > 0 && (player.Hero.Health - opponent.Hero.AttackDamage - opponent.BoardZone.Sum(p => p.AttackDamage) <= 3 || player.Hero.Health<10))
				//if (opt1.Count() > 0 && (player.Hero.Health - opponent.Hero.AttackDamage - opponent.BoardZone.Sum(p => p.AttackDamage) <= 6 ))
				if (opt1.Count() > 0 && (player.Hero.Health - opponent.Hero.AttackDamage - opponent.BoardZone.Sum(p => p.AttackDamage) <= 3 || player.Hero.Health < 10))
				{
					var tmp_game = game.getCopy();
					//Reno Jackson has 6 mana
					
					int mana = (tmp_game.CurrentPlayer.RemainingMana - 6) > 0 ? (tmp_game.CurrentPlayer.RemainingMana - 6) : 0;
					//Console.WriteLine("mana " + Convert.ToString(tmp_game.CurrentPlayer.BaseMana));

					var validOptsLoc = tmp_game.Simulate(options).Where(x => x.Value != null);
					var optcountLoc = validOptsLoc.Count();

					var score_resLoc = validOptsLoc.Select(x => score(x, player.PlayerId, (optcountLoc >= 5) ? ((optcountLoc >= 25) ? 1 : 2) : 3)).Where(x => (x.Key.Source == null ||x.Key.Source.Cost <= mana || x.Value == Int32.MaxValue)).Where(x => Convert.ToString(x.Key).Contains("Fireblast") != true || mana>=1).OrderBy(x => x.Value);
				
					/*Console.WriteLine("OPTS");
					
					foreach (var tmp_score in score_resLoc)
					{

						Console.WriteLine(Convert.ToString(tmp_score.Key) + Convert.ToString(tmp_score.Value));
					}
					Console.WriteLine("-------------------------------------------------------");
					
					Console.WriteLine("Round Nr:" + Convert.ToString(game.Turn));
					Console.WriteLine("HeroHP: " + Convert.ToString(player.Hero.Health) + "\tOppHP: " + Convert.ToString(game.CurrentOpponent.Hero.Health));
					Console.WriteLine("HeroMinionHP: " + Convert.ToString(player.BoardZone.Sum(p => p.Health)) + "\tOppMinionHP: " + Convert.ToString(game.CurrentOpponent.BoardZone.Sum(p => p.Health)));
					Console.WriteLine("HeroMinionAtk: " + Convert.ToString(player.BoardZone.Sum(p => p.AttackDamage)) + "\tOppMinionAtk: " + Convert.ToString(game.CurrentOpponent.BoardZone.Sum(p => p.AttackDamage)));
					Console.WriteLine("HeroNbMinions: " + Convert.ToString(player.BoardZone.Count) + "\tOppMinionNB: " + Convert.ToString(game.CurrentOpponent.BoardZone.Count));
					
					//Console.WriteLine("OPTS:" + Convert.ToString(score_res.First()));
					//Console.WriteLine("OPTS:" + Convert.ToString(score_res.Last()));
					//Console.WriteLine("OPTS:" + Convert.ToString(score_res.Count()));
					*/
					
					if (score_resLoc.Count() > 1)
					{

						//Console.WriteLine("OPTION TAKEN" + Convert.ToString(score_resLoc.Last().Key));

						return score_resLoc.Last().Key;

					}


					//Console.WriteLine("REEEEEEEEEEENNNNNNNNNNNOOOOOOOOOOOOOOO:" + Convert.ToString(player.Hero.Health));
					//Console.WriteLine("OPTS:" + Convert.ToString(opt1.First()));
					return opt1.First();
				}

				var returnValue = validOpts.Any() ?
					validOpts.Select(x => score(x, player.PlayerId, (optcount >= 5) ? ((optcount >= 25) ? 1 : 2) : 3)).OrderBy(x => x.Value).Where(x => Convert.ToString(x.Key).Contains("Reno Jackson") != true).Last().Key :
					player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);

				return returnValue;

				KeyValuePair<PlayerTask, int> score(KeyValuePair<PlayerTask, POGame> state, int player_id, int max_depth = 3)
				{
					int max_score = int.MinValue;
					if (max_depth > 0 && state.Value.CurrentPlayer.PlayerId == player_id)
					{
						var subactions = state.Value.Simulate(state.Value.CurrentPlayer.Options()).Where(x => x.Value != null);

						foreach (var subaction in subactions)
							max_score = Math.Max(max_score, score(subaction, player_id, max_depth - 1).Value);


					}
					max_score = Math.Max(max_score, Score(state.Value, player_id));
					return new KeyValuePair<PlayerTask, int>(state.Key, max_score);
				}
			}

			private int Score(POGame state, int playerId)
			{
				var p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;
				return new CustomScore { Controller = p }.Rate(RuntimeScaling);
			}

			public override void FinalizeAgent() { }
			public override void FinalizeGame() { }
			public override void InitializeAgent() { }
			public override void InitializeGame() { }
		}
		class CustomScore : Score.Score
		{
			public static double[] BaseScaling = new double[] {
				21.5, 33.6, 41.1, 19.4, 54, 60.5, 88.5,	84.7
			};
			public int HeroArmor => Controller.Hero.Armor;
			public int OpArmor => Controller.Opponent.Hero.Armor;
			public int Rate(double[] runtimeScaling)
			{
				if (OpHeroHp < 1)
					return Int32.MaxValue;

				if (HeroHp < 1)
					return Int32.MinValue;

				double score = 0.0;

				score += BaseScaling[0] * runtimeScaling[0] * (HeroHp + HeroArmor);
				score -= BaseScaling[1] * runtimeScaling[1] * (OpHeroHp + OpArmor);

				score += BaseScaling[2] * runtimeScaling[2] * BoardZone.Count;
				score -= BaseScaling[3] * runtimeScaling[3] * OpBoardZone.Count;

				score += BaseScaling[4] * runtimeScaling[4] * MinionTotHealth;
				score += BaseScaling[5] * runtimeScaling[5] * MinionTotAtk;

				score -= BaseScaling[6] * runtimeScaling[6] * OpMinionTotHealth;
				score -= BaseScaling[7] * runtimeScaling[7] * OpMinionTotAtk;

				return (int)Math.Round(score);
			}

			public override Func<List<IPlayable>, List<int>> MulliganRule()
			{
				return p => p.Where(t => t.Card.Name != "Reno Jackson" && t.Cost > 3).Select(t => t.Id).ToList();
			}
		}

		private const double DEFENSE_HEALTH_THRESHOLD = 1/3;

		private static double[] RuntimeScaling = Enumerable.Repeat(1.0, 8).ToArray();
		private AbstractAgent subAgent = null;
		private bool initialized = false;

		public override PlayerTask GetMove(POGame game)
		{
			if (!initialized)
				Initialize(game);

			// if there is a subagent
			if (subAgent != null)
				// let subagent do the move, skip the following code
				return subAgent.GetMove(game);

			var player = game.CurrentPlayer;

			// Implement a simple Mulligan Rule
			if (player.MulliganState == Mulligan.INPUT)
			{
				List<int> mulligan = new CustomScore().MulliganRule().Invoke(player.Choice.Choices.Select(p => game.getGame().IdEntityDic[p]).ToList());
				return ChooseTask.Mulligan(player, mulligan);
			}

			var opponent = game.CurrentOpponent;
			var options = player.Options();

			var coins = options.Where(x => x.HasSource && x.Source.Card.Name == "The Coin");
			if (coins.Count() > 0)
				return coins.First();

			var validOpts = game.Simulate(options).Where(x => x.Value != null);
			var optcount = validOpts.Count();
#if DEBUG
			/*
			var score_res = validOpts.Select(x => score(x, player.PlayerId, (optcount >= 5) ? ((optcount >= 25) ? 1 : 2) : 3)).OrderBy(x => x.Value);

				Console.WriteLine("Round Nr:" + Convert.ToString(game.Turn));
				Console.WriteLine("HeroHP: " + Convert.ToString(player.Hero.Health) + "\tOppHP: " + Convert.ToString(game.CurrentOpponent.Hero.Health));
				Console.WriteLine("HeroMinionHP: " + Convert.ToString(player.BoardZone.Sum(p => p.Health)) + "\tOppMinionHP: " + Convert.ToString(game.CurrentOpponent.BoardZone.Sum(p => p.Health)));
				Console.WriteLine("HeroMinionAtk: " + Convert.ToString(player.BoardZone.Sum(p => p.AttackDamage)) + "\tOppMinionAtk: " + Convert.ToString(game.CurrentOpponent.BoardZone.Sum(p => p.AttackDamage)));
				foreach (var tmp_score in score_res)
				{

					Console.WriteLine(Convert.ToString(tmp_score.Key) + Convert.ToString(tmp_score.Value));
				}
				Console.WriteLine("-------------------------------------------------------");
			//PrintLog(game,  score_res);
			*/
#endif

			if (player.Hero.Health < DEFENSE_HEALTH_THRESHOLD * player.Hero.BaseHealth)
				RuntimeScaling[0] += 0.1;

			if (opponent.Hero.Health < DEFENSE_HEALTH_THRESHOLD * opponent.Hero.Health)
				RuntimeScaling[1] += 0.1;

			var returnValue = validOpts.Any() ?
				validOpts.Select(x => score(x, player.PlayerId, (optcount >= 5) ? ((optcount >= 25) ? 1 : 2) : 3)).OrderBy(x => x.Value).Last().Key :
				player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);

			return returnValue;
			
			KeyValuePair<PlayerTask, int> score(KeyValuePair<PlayerTask, POGame> state, int player_id, int max_depth = 3)
			{
				int max_score = int.MinValue;
				if (max_depth > 0 && state.Value.CurrentPlayer.PlayerId == player_id)
				{
					var subactions = state.Value.Simulate(state.Value.CurrentPlayer.Options()).Where(x => x.Value != null);

					foreach (var subaction in subactions)
						max_score = Math.Max(max_score, score(subaction, player_id, max_depth - 1).Value);


				}
				max_score = Math.Max(max_score, Score(state.Value, player_id));
				return new KeyValuePair<PlayerTask, int>(state.Key, max_score);
			}
		}

		private void Initialize(POGame game)
		{
			switch (game.CurrentPlayer.HeroClass)
			{
				case CardClass.WARRIOR:
					subAgent = new WarriorSubAgent();
					break;
				case CardClass.MAGE:
					subAgent = new MageSubAgent();
					break;
				// default: just let subAgent be null so it skips the code for the "main" agent
			}

			initialized = true;
		}

		private int Score(POGame state, int playerId)
		{
			var p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;
			return new CustomScore { Controller = p }.Rate(RuntimeScaling);
		}

		public override void FinalizeAgent() {}
		public override void FinalizeGame() {}
		public override void InitializeAgent() {}
		public override void InitializeGame() {}
	}
}
