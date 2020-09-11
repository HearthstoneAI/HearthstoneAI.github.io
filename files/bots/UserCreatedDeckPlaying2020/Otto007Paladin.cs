using System;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
using System.Linq;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneBasicAI.Meta;



namespace SabberStoneBasicAI.AIAgents.Otto007
{
	class Otto007Paladin : AbstractAgent
	{//Disclaimer Teamwork with Joel Dierks and me(Lukas Schmidt)

	/* To Do
	 * adjust scoring algorithm for better results
	 * fiddle around with decks and heros
	 */

		private int currentTurn = 0;
		private bool debugFlag = false;
		private bool debugHighPriority = false;
		private int numGamesEndturnManaMoreThanTwo = 0;
		//lol there was a "bug" were the greedy score function would mark ending a turn as the best option
		private int numGamesEndturnManaMoreThanTwoTotal = 0;
		private int games = 0;

		public Otto007Paladin()
		{
			preferedHero = CardClass.PALADIN;
			preferedDeck = Decks.MidrangeBuffPaladin;
		}

		public override void InitializeAgent()
		{
			
		}

		public override void FinalizeAgent()
		{
			outputPriorityHigh("Total times more mana but end turn: " + numGamesEndturnManaMoreThanTwoTotal);
		}

		public override void FinalizeGame()
		{
			games++;
			outputPriorityHigh("game: " + games);
			//outputPriorityHigh("Times were turn ended but more mana left: " + numGamesEndturnManaMoreThanTwo);
			numGamesEndturnManaMoreThanTwo = 0;
		}

		public override PlayerTask GetMove(POGame poGame)
		{

			outputOptional("-----------new get Move-------------");

			initialDebug(poGame);
			debugBoardZone(poGame);

			Controller player = poGame.CurrentPlayer;
			List<double> scores = new List<double>();


			outputMyCards(poGame);

			if (player.MulliganState == Mulligan.INPUT)
			{
				outputOptional("\ntime for mulligan");
				List<int> mulligan = MulliganRule().Invoke(player.Choice.Choices.Select(p => poGame.getGame().IdEntityDic[p]).ToList());

				outputOptional("mulligan options:");
				for (int i = 0; i < mulligan.Count; i++)
				{
					outputOptional(mulligan[i].ToString());
				}


				outputOptional(ChooseTask.Mulligan(player, mulligan).ToString());
				//outputOptional("muligan choices: " + player.Choice.Choices.ToString());

				return ChooseTask.Mulligan(player, mulligan);
			}

			

			List<PlayerTask> options = poGame.CurrentPlayer.Options();
			
			debugOptions(options, player);

			if (options.Count == 1)
			{
				if (options[0].PlayerTaskType == PlayerTaskType.END_TURN)
				{
					outputOptional("ending turn because only one end turn option available");
					outputOptional("########### New Round ###########");
					currentTurn += 1;
					return options[0];
				}
				else
				{
					outputPriorityHigh("[Error]: only one option left but its not ending a turn");
					return options[0];
				}
			}
			var simulated = poGame.Simulate(options);
			
				foreach (var s in simulated)
					// s.Value is the POGame of that simulated move
					scores.Add(Score(s.Key, s.Value));
			

			//debugScoringOptions(simulated, scores);
			debugSimScoringOptions(simulated, scores);

			int highestScoringIndex = HighestValue(scores);
			if (highestScoringIndex == 0 && player.RemainingMana > 1)
			{
				numGamesEndturnManaMoreThanTwo++;
				numGamesEndturnManaMoreThanTwoTotal++;
				outputPriorityHigh("m8 premature end of turn: Mana:" + player.RemainingMana.ToString() + "|"+  options[0].ToString());
			}
			outputOptional("choosing this task: " + options[highestScoringIndex].ToString() + " at " + highestScoringIndex);

			return options[highestScoringIndex];
		}

		public override void InitializeGame()
		{
		}


		public Func<List<IPlayable>, List<int>> MulliganRule()
		{// not random anymore, discard all cards with mana cost > 3 bc we start off with little mana
			return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
		}

		public double Score(PlayerTask task, POGame game)
		{
			//outputOptional("[Scoring]: -- scoring called |TaskType: " + task.PlayerTaskType + "--");
			double score = 0;
			double[] weights = { 4, 1, 1.2 , 1.2 ,1, 0.5 , 1 , 3 , 4};
			int playerId = game.CurrentPlayer.PlayerId;
			Controller player = game.CurrentPlayer.PlayerId == playerId ? game.CurrentPlayer : game.CurrentOpponent;
			Controller enemy = game.CurrentPlayer.PlayerId == playerId ? game.CurrentOpponent : game.CurrentPlayer;
			// the problem was that I was only looking at the cards options but not at the overall pogame

			double totalMinAtkMe = totalMinion(player, "a");
			double totalMinAtkOp = totalMinion(enemy, "a");

			double totalMinHMe = totalMinion(player, "h");
			double totalMinHOp = totalMinion(enemy, "h");


			//score += player.Hero.Health;
			//score -= enemy.Hero.Health;
			if (totalMinAtkMe >= enemy.Hero.Health)
				score += 20000;
			score -= enemy.BoardZone.Count() * weights[0]; // I want the enemy to have low board card count
			//score += totalMinAtkMe * weights[1];
			//score += totalMinHMe * weights[1];

			score += (totalMinAtkMe - totalMinAtkOp) * 20;
			score += (player.Hero.Health - enemy.Hero.Health) * 30;

			score += (totalMinHMe - totalMinHOp) * 10;


			//score -= totalMinAtkOp * weights[2];
			//score -= totalMinHOp * weights[3];

			// and then I can look at the specific tasks effect on other cards
			if (task.PlayerTaskType == PlayerTaskType.END_TURN)
			{
				//outputOptional("[Scoring]: return MinValue because its end turn");
				return double.MinValue;
			}

			
			if (task.HasSource)
			{ //sonst wirft es exceptions, ob es eine Karte ist
				if (task.PlayerTaskType == PlayerTaskType.PLAY_CARD)
				//Karte spielen, da gibt es Spells oder Minions
				{
					Card card = task.Source.Card;
					//outputOptional(task.ToString());
					//outputOptional("[Scoring]: this is the task" + task.ToString());

					if (card.Name.Contains("Coin")) // the coin that gives us one extra mana, when starting second
					{
						outputPriorityHigh("[Rare]: caught the coin");
						foreach (var handCard in player.HandZone)
						{
							if (handCard.Card.Type == CardType.MINION && handCard.Cost == player.RemainingMana-1) // the coin gives you one extra mana
								score += 1000;
						}
					}

					if (card.Type == CardType.SPELL)
					{// Spell begin
						if (task.HasTarget)
						{
							if (enemy.BoardZone.Contains(task.Target))
							{
								if (card.ATK == task.Target.Health)
								{
										// if we can clear opponents card in one go
										//score += task.Target.Health;
									score += task.Target.AttackDamage + (task.Target.Health/ task.Target.AttackDamage)*weights[5];
								}
								else if (card.ATK < task.Target.Health)
								{
									//bad choice because we should clear opponents cards in one go,
										//score += task.Target.AttackDamage*weights[6];
									
								}
								else if (card.ATK > task.Target.Health)
								{//kill enemey card in one go (spell card) BUT wasting attack

									//score += task.Target.AttackDamage *(task.Target.Health / card.ATK)* weights[7];
								}
							}

						}
						else // es hat kein target, also eine Karte die großflaechig damage macht
						{
							//score += enemy.BoardZone.Count*weights[8]; // da flaechen damage schauen wie viele wir treffen koennen
						}
					}//Spell end

					if (card.Type == CardType.MINION) // playing a card from hand // not minion attack!
					{//minion type
						if (card.Health <= 0)
						{
							//score += (task.Target.Cost - card.Cost);
						}
							//outputOptional("[Scoring]: Minion Card" + card.ToString() + "| Cost: " + card.Cost + " | Remaining Mana: " + player.RemainingMana);
						score += player.RemainingMana / card.Cost; // this would prefer low cost cards but in late game high cost cards are good?
					}
					//Console.WriteLine("[Scoring]: calculated the score for " + card.ToString() + " : " + score);
				}
				if (task.PlayerTaskType == PlayerTaskType.HERO_ATTACK || task.PlayerTaskType == PlayerTaskType.HERO_POWER) // oder Hero Power?
				{//better use mana for playing cards
					//outputOptional("[Scoring]: caught a Hero attack/power " + task.ToString());
					if (task.Target.Health > weights[9]) // I would like to have player.HeroAttackPower * 2
												// so if it would take more than 2 activations of the hero power to kill the target punish it
												// if less or equal to 2 turn then go for it
												// differentiate between minions and enemy hero
					{
									//score -= task.Target.Health;
						score -= task.Target.Health;
					}
					else
					{
								//score += task.Target.Health; //maybe somehting better than just the target.Health?
						//score += task.Target.Health*(task.Target.AttackDamage+1); 
					}
				}

				if (task.PlayerTaskType == PlayerTaskType.MINION_ATTACK) 
				{
						//outputOptional("[Scoring]: caught a Minion Attack " + task.ToString());
					//score += task.Target.Health + task.Target.AttackDamage; // attack enemies with more health first
						//but maybe better to also consider bots with low health but high damage
				}


				//outputOptional("[Scoring]: pre calculated the score for task " + task.ToString() + " : " + Math.Round(score,2));
			}




			//outputOptional("[Scoring]: ---- final score calculated: " + Math.Round(score,2) + "---");

			return score;
		}

		private double totalMinion(Controller ctrl,string type)
		{
			double s = 0;
			if (type == "a")
			{
				foreach (var c in ctrl.BoardZone)
					s += c.AttackDamage;
			}
			if (type == "h")
			{
				foreach (var c in ctrl.BoardZone)
					s += c.Health;
			}
			return s;
		}

		//

		public int HighestValue(List<double> l)
		{//there is probably a built in function for this but I don't have internet right now to check
			int highestIndex = 0;
			double highestValue = double.MinValue;
			for (int i = 0; i < l.Count; i++)
			{
				if (l[i] > highestValue)
				{
					highestValue = l[i];
					highestIndex = i;
				}
			}
			
			outputOptional("[Highest Scoring]: " + Math.Round(highestValue,2) + " at " + highestIndex);
			if (highestIndex == 0) outputPriorityHigh("highest index is 0");
			return highestIndex;
		}

		//debugging functions

		public void initialDebug(POGame poGame)
		{
			if (debugFlag)
			{
				Console.WriteLine("|-----Initial Debug-------");
				var player = poGame.CurrentPlayer;
				//Console.WriteLine("DeckCards: " + player.DeckCards.ToString());
				Console.WriteLine("Remaining Mana: " + player.RemainingMana);
				Console.WriteLine("NumCardsDrawnThisTurn: " + player.NumCardsDrawnThisTurn);
				Console.WriteLine("NumCardsPlayedThisTurn: " + player.NumCardsPlayedThisTurn);
				Console.WriteLine("NumCardsToDraw: " + player.NumCardsToDraw);
				Console.WriteLine("Turn: " + poGame.Turn);
				Console.WriteLine("currentTurn variable buggy?: " + currentTurn);
				//Console.WriteLine("wierd Hero thing: " + poGame.Heroes.ToString());
				Console.WriteLine("|------Initial Debug End------");
			}
		}

		public void outputOptional(string outs)
		{
			if (debugFlag)
			{
				Console.WriteLine(outs);
			}
		}

		public void outputPriorityHigh(string outs)
		{
			if (debugHighPriority)
			{
				Console.WriteLine(outs);
			}
		}

		public void outputMyCards(POGame poGame)
		{
			if (debugFlag)
			{
				var player = poGame.CurrentPlayer;
				int playerId = poGame.CurrentPlayer.PlayerId;
				Controller playerC = poGame.CurrentPlayer.PlayerId == playerId ? poGame.CurrentPlayer : poGame.CurrentOpponent;
				Controller enemyC = poGame.CurrentPlayer.PlayerId == playerId ? poGame.CurrentOpponent : poGame.CurrentPlayer;

				Console.WriteLine("--- My Cards on Hand ---");

				foreach (var c in player.HandZone)
				{
					//Console.WriteLine(item + " Attack: " + item + " |Mana: " +item.Cost);
					Console.WriteLine(c+ "|Mana: " + c.Cost);
				}

				Console.WriteLine("------      ------");
				Console.WriteLine(playerC.HeroClass.ToString());
				Console.WriteLine("\n");
			}
		}

		public void debugBoardZone(POGame poGame)
		{
			if (debugFlag)
			{
				Console.WriteLine("\n		BoardZone Info: ");
				var player = poGame.CurrentPlayer;
				int playerId = poGame.CurrentPlayer.PlayerId;
				Controller playerC = poGame.CurrentPlayer.PlayerId == playerId ? poGame.CurrentPlayer : poGame.CurrentOpponent;
				Controller enemyC = poGame.CurrentPlayer.PlayerId == playerId ? poGame.CurrentOpponent : poGame.CurrentPlayer;

				//enemyC.BoardZone
				Console.WriteLine("My BoardZone aka played Cards");
				foreach (var playedCard in playerC.BoardZone)
				{
					Console.WriteLine("[BoardZone ME]:" + playedCard + "|Health: " + playedCard.Health + " |Damage:" + playedCard.AttackDamage);
				}
				Console.WriteLine("Enemy BoardZone aka played Cards");
				foreach (var playedCard in enemyC.BoardZone)
				{
					Console.WriteLine("[BoardZone OP]:" + playedCard + "|Health: " + playedCard.Health + " |Damage:" + playedCard.AttackDamage);
				}
				Console.WriteLine("		BoardZone Info End\n ");

			}
		}
		public void debugOptions(List<PlayerTask> options, Controller player)
		{
			if (debugFlag)
			{
				Console.WriteLine("\nprinting " + options.Count + " options this round | Mana:" + player.RemainingMana);
				for (int i = 0; i < options.Count; i++)
				{
					Console.WriteLine(options[i].ToString() + " " + options[i].PlayerTaskType + " " + debugManaCostOption(options[i]).ToString());
				}
				Console.WriteLine("\n");
			}
		}

		public string debugManaCostOption(PlayerTask o)
		{
			if (o.HasSource)
			{
				return o.Source.Card.Cost.ToString();
			}
			else //if (o.PlayerTaskType == PlayerTaskType.HERO_POWER)
			{
				Console.WriteLine("getting mana debug: " + o.ToString());
				return " Nan";
			}
			return " Nan"; // HeroPower is not caught
		}

		public void debugScoringOptions(List<PlayerTask> o, List<double> s)
		{
			if (debugFlag)
			{
				Console.WriteLine("\nscoring scoring scoring scoring");
				if (o.Count == s.Count)
				{
					for (int i = 0; i < o.Count; i++)
					{
						Console.WriteLine(o[i].ToString() + " |" + s[i]);
					}
				}
				Console.WriteLine("\n");
			}
		}

		public void debugSimScoringOptions(Dictionary<PlayerTask,POGame> o, List<double> s)
		{
			if (debugFlag)
			{
				Console.WriteLine("\nscoring scoring- Debug Simulated scorings options -scoring scoring");
				if (o.Count == s.Count)
				{
					var c = 0;
					foreach(var i in o)
					{
						Console.WriteLine(i.Key.ToString() + " is:" + Math.Round(s[c],2));
						c++; // lol C++ in C#
					}
				}
				Console.WriteLine("\n");
			}
		}

		public void playedCards(POGame poGame)
		{
			if (debugFlag)
			{
				int playerId = poGame.CurrentPlayer.PlayerId;
				Controller playerC = poGame.CurrentPlayer.PlayerId == playerId ? poGame.CurrentPlayer : poGame.CurrentOpponent;
				Controller enemyC = poGame.CurrentPlayer.PlayerId == playerId ? poGame.CurrentOpponent : poGame.CurrentPlayer;
				Console.WriteLine(playerC.LastCardPlayed);
			}
		}
	}
}
