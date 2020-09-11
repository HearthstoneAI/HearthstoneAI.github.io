using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneBasicAI.Score;
using SabberStoneBasicAI.Meta;
using SabberStoneCore.Model.Entities;


// TODO choose your own namespace by setting up <submission_tag>
// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.Shrouded
{
	/*TASK PRIORITY POGGERS
	 * CHOOSETASK
	 * PLAYCARDTASK
	 * HEROPOWERTASK
	 * HEROATTACKTASK AND MINIONATTACKTASK
	 * ENDTURNTASK
	 * CONCEDETASK (why do I even mention that? defeat is not an Option)
	 */
	class ShroudedYmir : AbstractAgent
	{
        public ShroudedYmir()
        {
            preferedDeck = Decks.RenoKazakusMage;   //deck which should be played
            preferedHero = CardClass.MAGE; //hero class of the chosen deck
        }

        public override void InitializeGame()
		{
		}

		public override void InitializeAgent()
		{
		}

		public override void FinalizeAgent()
		{
		}

		public override void FinalizeGame()
		{
		}

		public override PlayerTask GetMove(POGame poGame)
		{
			var player = poGame.CurrentPlayer;

			var validOpts = poGame.Simulate(player.Options()).Where(x => x.Value != null);

			return validOpts.Any() ?
				validOpts.OrderBy(x => Score(x.Value, player.PlayerId)).Last().Key :
				player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);

		}

		// Calculate different scores based on our hero's class
		private static int Score(POGame state, int playerId)
		{
			var p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;

			return new ShroudedScore3 { Controller = p }.Rate();
		}

		

		public class ShroudedScore3 : SabberStoneBasicAI.Score.Score
		{
			public override int Rate()
			{
				if (OpHeroHp < 1)
					return int.MaxValue;

				if (HeroHp < 1)
					return int.MinValue;

				int result = 0;

				if (OpBoardZone.Count == 0 && BoardZone.Count > 0)
					result += 5000;

				result += (BoardZone.Count - OpBoardZone.Count) * 5;

				if (HeroAtk > 0)
					result += HeroAtk * 100;

				if (OpMinionTotHealthTaunt > 0)
					result += OpMinionTotHealthTaunt * -10;

				if (MinionTotHealthTaunt > 0)
					result += OpMinionTotHealthTaunt * 100;

				result += MinionTotAtk * 10;

				result += (HeroHp - OpHeroHp) * 10;

				result += (MinionTotHealth - OpMinionTotHealth) * 10;

				result += (MinionTotAtk - OpMinionTotAtk) * 100;

				return result;
			}

			public override Func<List<IPlayable>, List<int>> MulliganRule()
			{
				return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
			}
		}

		/*public override PlayerTask GetMove(POGame poGame)
		{
			List<PlayerTask> options = poGame.CurrentPlayer.Options();
			var Simulations = poGame.Simulate(options);
			List<ChooseTask> chooseTasks = new List<ChooseTask>();
			List<ConcedeTask> concedeTasks = new List<ConcedeTask>();
			List<EndTurnTask> endTurnTasks = new List<EndTurnTask>();
			List<HeroAttackTask> heroAttackTasks = new List<HeroAttackTask>();
			List<HeroPowerTask> heroPowerTasks = new List<HeroPowerTask>();
			List<MinionAttackTask> minionAttackTasks = new List<MinionAttackTask>();
			List<PlayCardTask> playCardTasks = new List<PlayCardTask>();
			List<PlayerTask> invalidTasks = new List<PlayerTask>();
			foreach (var simulation in Simulations)
			{
				if (simulation.Value != null)
				{
					switch (simulation.Key)
					{
						case ChooseTask task:
							chooseTasks.Add(task);
							break;
						case ConcedeTask task:              //DEFEAT OR SURRENDER WAS NEVER AN OPTION
							concedeTasks.Add(task);
							break;
						case EndTurnTask task:
							endTurnTasks.Add(task);
							break;
						case HeroAttackTask task:
							heroAttackTasks.Add(task);
							break;
						case HeroPowerTask task:
							heroPowerTasks.Add(task);
							break;
						case MinionAttackTask task:
							minionAttackTasks.Add(task);
							break;
						case PlayCardTask task:
							playCardTasks.Add(task);
							break;
						default:
							invalidTasks.Add(simulation.Key);
							break;
					}
				}
			}

			if (chooseTasks.Count != 0)
			{
				return evaluateChoosetasks(chooseTasks);
			}
			PlayCardTask taski;
			if (playCardTasks.Count > 0)
			{
				taski = evaluatePlayCardTasks(playCardTasks);
				if (taski != null) return taski;
			}
			if ((heroAttackTasks.Count > 0) || (minionAttackTasks.Count > 0))
			{
				PlayerTask task;
				task = evaluateAttackTasks(minionAttackTasks, heroAttackTasks, poGame);        //further evaluate the options and attack the target thats most efficient
				if (task != null) return task;
			}
			return endTurnTasks[0];
		}

		public ChooseTask evaluateChoosetasks(List<ChooseTask> chooseTasks)
		{
			//possible change: different choosing tasks from the one at the start of the game that could probably damage me
			//but as I know nothing about Hearthstone this is probably not gonna happen
			//(only when I try extra hard)
			int maxArgLength = -1;
			foreach (var task in chooseTasks)
			{
				int i = 0;
				foreach (var choice in task.Choices)
				{
					i++;
				}
				if (i > maxArgLength) maxArgLength = i;
			}
			List<ChooseTask> goodOptions = new List<ChooseTask>();
			foreach (var task in chooseTasks)
			{
				if (task.Choices.Count == maxArgLength)
				{
					goodOptions.Add(task);
				}
			}

			return goodOptions[0];

			/*List<ChooseTask> bestOptions = new List<ChooseTask>();
			bestOptions.Add(goodOptions[0]);
			foreach(var task in goodOptions)
			{
				if(task.HasTarget)
				{
					if(task.Target.AttackDamage > bestOptions[0].Target.AttackDamage)
					{
						bestOptions = new List<ChooseTask>();
						bestOptions.Add(task);
					}
					else if(task.Target.AttackDamage == bestOptions[0].Target.AttackDamage)
					{
						bestOptions.Add(task);
					}
				}
			}
			
			return bestOptions[0];*/
		/*}

		public PlayCardTask evaluatePlayCardTasks(List<PlayCardTask> playCardTasks)
		{
			/*something to try to improve this
			 * add a variable that gets the number of turns since the last big card was played
			 * play the card with the most attack value for the current mana if no minion is left
			 */


		/*foreach (var task in playCardTasks)
		{
			if(task.Source.Card.Name.Contains("Coin"))
			{
				foreach (var item in task.Controller.HandZone)
				{
					if ((item.Card.Type == SabberStoneCore.Enums.CardType.MINION || item.Card.Type == SabberStoneCore.Enums.CardType.WEAPON) && item.Cost == task.Controller.RemainingMana)
						return task;
				}
			}
			else if ((task.Source.Card.Cost == 0))
			{
				return task;
			}
		}

		foreach (var task in playCardTasks)
		{
			if (task.HasSource)
			{
				if ((task.Source.Card.Type == SabberStoneCore.Enums.CardType.WEAPON))
				{
					SabberStoneCore.Model.Entities.Weapon weapon = (SabberStoneCore.Model.Entities.Weapon)task.Source;
					if (task.Controller.Hero.Weapon == null)
						return task;
					else if ((task.Controller.Hero.Weapon.Durability < 2) && (task.Controller.BoardZone.Count > 1))
						return task;
					else if ((weapon.Damage - task.Controller.Hero.Weapon.Damage) > 3)
						return task;
				}
			}
		}


		List<PlayCardTask> bestAttackPlays = new List<PlayCardTask>();
		int highestAttack = -1;
		foreach (var task in playCardTasks)
		{
			if (task.Source.Card.ATK > highestAttack)
			{
				bestAttackPlays = new List<PlayCardTask>();
				bestAttackPlays.Add(task);
				highestAttack = task.Source.Card.ATK;
			}
			else if(task.Source.Card.ATK == highestAttack)
			{
				bestAttackPlays.Add(task);
			}
		}
		foreach(var card in playCardTasks[0].Controller.HandZone)
		{
			if ((card.Card.ATK > highestAttack) && (card.Card.Cost == (card.Controller.RemainingMana + 1)) && ((card.Controller.BoardZone.Count > 0) || card.Controller.Opponent.BoardZone.Count < 1))
				return null;
		}
		int lowestManaCost = 50;
		List<PlayCardTask> bestManaPlays = new List<PlayCardTask>();
		foreach(var task in bestAttackPlays)
		{
			if (task.Source.Card.Cost < lowestManaCost)
			{
				bestManaPlays = new List<PlayCardTask>();
				bestManaPlays.Add(task);
				lowestManaCost = task.Source.Card.Cost;
			}
			else if (task.Source.Card.Cost == lowestManaCost)
				bestManaPlays.Add(task);
		}
		List<PlayCardTask> bestHealthPlays = new List<PlayCardTask>();
		int maxHealth = -1;
		foreach(var task in bestManaPlays)
		{
			if (task.Source.Card.Health > maxHealth)
			{
				bestHealthPlays = new List<PlayCardTask>();
				bestHealthPlays.Add(task);
				maxHealth = task.Source.Card.Health;
			}
			else if (task.Source.Card.Health == maxHealth)
				bestHealthPlays.Add(task);
		}
		return bestHealthPlays[0];
	}

	public PlayerTask evaluateAttackTasks(List<MinionAttackTask> minionAttacks, List<HeroAttackTask> heroAttacks, POGame poGame)
	{
		if (minionAttacks.Count != 0)
		{
			double minionAttackDamageOnHero = 0;
			foreach (var task in minionAttacks)
			{
				if((task.Target.ZonePosition == poGame.CurrentOpponent.Hero.ZonePosition) && (task.Source.ZonePosition < poGame.CurrentPlayer.BoardZone.Count))
				{
					minionAttackDamageOnHero += poGame.CurrentPlayer.BoardZone[task.Source.ZonePosition].AttackDamage;
				}
			}
			foreach (var task in minionAttacks)
			{
				if ((task.Target.ZonePosition == poGame.CurrentOpponent.Hero.ZonePosition) && (minionAttackDamageOnHero > poGame.CurrentOpponent.Hero.Health)) return task;
				else if ((task.Target.ZonePosition == poGame.CurrentOpponent.Hero.ZonePosition) && (poGame.CurrentOpponent.Hero.Health < 7)) return task;
				else if ((task.Target.ZonePosition == poGame.CurrentOpponent.Hero.ZonePosition) && (poGame.CurrentOpponent.BoardZone.Count < 2))
				{
					return task;
				}
			}
			List<Tuple<SabberStoneCore.Model.Entities.Minion, List<MinionAttackTask>>> enemyBeatenBy = new List<Tuple<SabberStoneCore.Model.Entities.Minion, List<MinionAttackTask>>>();
			int i = 0;
			foreach (var enemyMinion in poGame.CurrentOpponent.BoardZone)
			{
				enemyBeatenBy.Add(new Tuple<SabberStoneCore.Model.Entities.Minion, List<MinionAttackTask>>(enemyMinion, new List<MinionAttackTask>()));
				foreach (MinionAttackTask task in minionAttacks)
				{
					if (task.Target.ZonePosition == enemyMinion.ZonePosition)
					{
						if ((poGame.CurrentPlayer.BoardZone.Count > 0) && (poGame.CurrentPlayer.BoardZone.Count > task.Source.ZonePosition))
						{
							if (poGame.CurrentPlayer.BoardZone[task.Source.ZonePosition].AttackDamage >= poGame.CurrentOpponent.BoardZone[task.Target.ZonePosition].Health)
							{
								enemyBeatenBy[i].Item2.Add(task);
							}
						}
					}
				}
				i++;
			}
			bool isEnemyBeatenByEmpty = true;
			enemyBeatenBy.OrderBy(x => x.Item1.Health);
			foreach (var tuple in enemyBeatenBy)
			{
				if (tuple.Item2.Count > 0)
				{
					isEnemyBeatenByEmpty = false;
				}
			}

			if (!isEnemyBeatenByEmpty)
			{
				foreach (var tuple in enemyBeatenBy)
				{
					if (tuple.Item2.Count > 0)
					{
						var lowestAttack = tuple.Item2[0];
						foreach (var task in tuple.Item2)
						{
							if (poGame.CurrentPlayer.BoardZone[task.Source.ZonePosition].AttackDamage < poGame.CurrentPlayer.BoardZone[lowestAttack.Source.ZonePosition].AttackDamage)
							{
								lowestAttack = task;
							}
						}
						return lowestAttack;
					}
				}
			}
			else
			{
				if (poGame.CurrentOpponent.BoardZone.Count > 0)
				{
					var lowestHPminion = poGame.CurrentOpponent.BoardZone[0];
					foreach (var minion in poGame.CurrentOpponent.BoardZone)
					{
						if (lowestHPminion.Health < minion.Health)
						{
							lowestHPminion = minion;
						}
					}
					var lowestOwnATKminion = minionAttacks[0];
					foreach (var task in minionAttacks)
					{
						if (task.Target.ZonePosition == lowestHPminion.ZonePosition)
						{
							if ((poGame.CurrentPlayer.BoardZone.Count > 0) && (poGame.CurrentPlayer.BoardZone.Count > task.Source.ZonePosition))
							{
								if (poGame.CurrentPlayer.BoardZone[task.Source.ZonePosition].AttackDamage > poGame.CurrentPlayer.BoardZone[lowestOwnATKminion.Source.ZonePosition].AttackDamage)
								{
									lowestOwnATKminion = task;
								}
							}
						}
					}
					return lowestOwnATKminion;
				}
			}
		}
		else
		{
			foreach (var task in heroAttacks)
			{
				if (task.Target.ZonePosition == poGame.CurrentOpponent.Hero.ZonePosition)
					return task;
				else
					return heroAttacks[0];
			}
		}
		if (minionAttacks.Count != 0) return minionAttacks[0];
		else return heroAttacks[0];
	}

	public override void InitializeGame()
	{
	}*/
	}
}
