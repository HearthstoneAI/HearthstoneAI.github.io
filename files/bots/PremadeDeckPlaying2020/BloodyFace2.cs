using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneBasicAI.Score;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
//Allows List<T> to work
using System.Collections.Generic;
using SabberStoneCore.Model.Zones;

// TODO choose your own namespace by setting up <submission_tag>
// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.BloodyFace
{
	class BloodyFace2 : AbstractAgent
	{

		private int moveNum;
		private int currTurn;
		private static List<Minion> playerList;
		private static List<Minion> opponentList;
		private static List<MinionID> TurnTaskList;
		private int turnTaskIndex;
		public struct MinionID {
			public Minion playerMinion {get; set;}
			public Minion opponentMinion {get; set;}
		}
		private static List<IPlayable> minionList;
		private static List<IPlayable> spellList;
		private static List<IPlayable> weaponList;
		public override void InitializeAgent()
		{ 
		}
		
		public override void InitializeGame() { 
			//Console.WriteLine("In intialize game - and setting up Class variables");
			moveNum = 1;
			currTurn = 0;
		}

		public override void FinalizeAgent()
		{ }

		public override void FinalizeGame()
		{ }

		private List<Minion> GetMinions(POGame game, Controller owner){

			List<Minion> minionList = new List<Minion>();

			foreach (var m in game.Minions)
			{
				if(m.Controller == owner){
					//Console.WriteLine($"{m.Controller}'s {m} does damage/health - [{m.AttackDamage}-{m.Health}]");
					minionList.Add(m);
				}	
			}
			return minionList;
		}
		//Choice cards***can choose a task when you play it
		private PlayerTask PlayCard(List<PlayerTask> PlayerTaskList, Controller myself, HandZone myHand, BoardZone theBoard, POGame game){ 
	
			PlayerTask returnTask = null;
			int maxManaCard = 0;
			int HighestDamage = 0;
			int WeaponAttackDamage = 0;
			//For each valid option, prints all available moves and the cost of the card		
			foreach (PlayerTask task in PlayerTaskList){
				if(task.PlayerTaskType == PlayerTaskType.PLAY_CARD){
					/*if(task.Target != null){
						Console.WriteLine($"Can Play: {task} Mana: {task.Source.Card.Cost} Target Controller: {task.Target.Controller} Type: {task.Source.Card.Type}");
					}
					else{
						Console.WriteLine($"Can Play {task}, {task.Source.Card.Cost} mana. Type: {task.Source.Card.Type}");
					}*/

					var cardType = task.Source.Card.Type;
					if (game.CurrentPlayer.RemainingMana > 0){
						if (cardType == CardType.MINION){
							//Console.WriteLine(task);
							//Console.WriteLine($"Entered if statement 0");
							if (task.Source.Card.Taunt == true){
								//Console.WriteLine($"Entered if statement 1");
								if (theBoard.Count() >= 1){
									//Console.WriteLine($"Entered if statement 2");
									returnTask = task;							
								}
							}
							else if (task.Source.Card.Charge == true || task.Source.Card.Rush == true && task.PlayerTaskType == PlayerTaskType.PLAY_CARD && task.Source.Card.Cost >= maxManaCard){
								//Adds to the task list because these  minions can attack in the same turn
								//Console.WriteLine($"Entered if statement 3");
								maxManaCard = task.Source.Card.Cost;
								returnTask = task;
								var mi = new MinionID(); //mi = minion id 
								Minion PlayerMinion = null;
								Minion OpponentMinion = null;
								foreach (var x in playerList){
									if (x.ToString().Equals(task.Source.ToString())){
										PlayerMinion = x;
									}
								}
								int Damage = 0;
								foreach (var y in opponentList){
									if (y.AttackDamage > Damage){
										Damage = Damage + y.AttackDamage;
										OpponentMinion = y;
									}
								}
								mi.playerMinion = PlayerMinion;
								mi.opponentMinion = OpponentMinion;
								TurnTaskList.Add(mi);
								continue;
							} 
							else {
								if (task.PlayerTaskType == PlayerTaskType.PLAY_CARD && task.Source.Card.Cost >= maxManaCard){
									maxManaCard = task.Source.Card.Cost;
									returnTask = task;
								}
							}
						}
						else if (cardType == CardType.SPELL && task.Target != null && task.Target.Controller != myself && task.Target.Health > 0) {
							//Better way to verify spell target by ID
							for (int i = 0; i < opponentList.Count(); i++){
								if (opponentList[i].Id == task.Target.Id){
									returnTask = task; 
								} 
								else if (task.Target.AttackDamage > HighestDamage){
									HighestDamage = task.Target.AttackDamage;
									returnTask = task;
								}
							}
							if (task.Target.Id == game.CurrentOpponent.Hero.Id){
								returnTask = task;
							}
							continue;
						}
						else if (cardType == CardType.WEAPON) {
							if (task.Source.Card.ATK > WeaponAttackDamage){
									WeaponAttackDamage = task.Source.Card.ATK;
									returnTask = task;
							}
						}
						// can't explicitly target own minions and hero
						else if(task.Target == null){
							maxManaCard = task.Source.Card.Cost;
							returnTask = task;
						}		
					}	
					else if (myHand.Count() == 1){
						returnTask = task;
					}
					else {
						//Console.WriteLine($"No play card task available");
					}
				}	
			}
	

			/*if(returnTask == null){
				Console.WriteLine("Return Playcard task: No card selected");
			}
			else{
				Console.WriteLine($"Return PlayCard task: {returnTask}");
			}*/

			return returnTask;

		}

		private void InspectHand(HandZone myHand){
			//Prints out player's hand in classified groups
			minionList = new List<IPlayable>();
			weaponList = new List<IPlayable>();
			spellList = new List<IPlayable>();

			foreach (var x in myHand){
				if (x.Card.Type == CardType.MINION){
					minionList.Add(x);
				}
				else if(x.Card.Type == CardType.WEAPON){
					weaponList.Add(x);
				}
				else if(x.Card.Type == CardType.SPELL){
					spellList.Add(x);
				}
			}
			/*foreach (var x in minionList) {
				Console.WriteLine($"Minion: {x}");
			}
			foreach (var y in weaponList){
				Console.WriteLine($"Weapon: {y}");
			}
			foreach (var z in spellList){
				Console.WriteLine($"Spell: {z}");
			}*/
		}

		private PlayerTask HeroPower(List<PlayerTask> PlayerTaskList, Controller myself){
			PlayerTask returnTask = null;
			foreach (PlayerTask task in PlayerTaskList){
				if(task.PlayerTaskType == PlayerTaskType.HERO_POWER && task.Controller.Hero.HeroPower.Cost <= myself.RemainingMana){
					//Console.WriteLine($"Hero power: {task}");
					//Console.WriteLine($"I have {myself.RemainingMana} mana remaining");
					//Console.WriteLine($"Hero Power {task.Controller.Hero.HeroPower} costs {task.Controller.Hero.HeroPower.Cost} mana");
					returnTask = task;
				}
			}
			/*if(returnTask == null){
				Console.WriteLine("Return Heropower task: No card selected");
			}
			else{
				Console.WriteLine($"Return Heropower task: {returnTask}");
			}*/

			return returnTask;
		}

		private void TurnTasks(POGame game, Controller player) {
			TurnTaskList = new List<MinionID>();
			playerList = GetMinions(game, game.CurrentPlayer);
			opponentList = GetMinions(game, game.CurrentOpponent);
			//Lowest Attack Damage --> Highest Attack Damage
			List<Minion> sortedPlayerList = playerList.OrderBy(x => x.AttackDamage).ToList();
			//Highest --> Lowest attack damage
			List<Minion> desSortedPlayerList = playerList.OrderByDescending(x => x.AttackDamage).ToList();
			//Highest Attack Damage --> Lowest Attack Damage
			List<Minion> sortedOpponentList = opponentList.OrderByDescending(x => x.AttackDamage).ToList();
			List<Minion> usedList = new List<Minion>();

		/*	foreach (var o in sortedOpponentList){
				Console.WriteLine($"Here is opponent minion {o} which has {o.Health} health and does {o.AttackDamage} damage");
			}
			foreach (var p in sortedPlayerList){
				Console.WriteLine($"Here is player minion {p} which has {p.Health} health and does {p.AttackDamage} damage");
			}*/
			foreach (var o in sortedOpponentList){
				int oHealth = o.Health;
				int DamageCounter = 0;
				int index = 0;
				//Console.WriteLine($"The opponent minion is {o} which has {oHealth} health.");
				//If I can play a card, it should play it if I can get rid of a minion in one move
				foreach (var p in desSortedPlayerList){
					if (!usedList.Contains(p) && p.AttackDamage != 0) {
						usedList.Add(p);
						var diff = p.AttackDamage - oHealth;
						if (diff <= 3 && diff >= 0){ //3 is an arbitrary number to not use a 9 damage card on a 1 health opponent
							index++;
							var mi = new MinionID();
							mi.playerMinion = p;
							mi.opponentMinion = o;
							TurnTaskList.Add(mi);
						}
					}
				}
				//Otherwise, count of the damage to see the minimum amount of minions to kill a player. 
				foreach (var p in sortedPlayerList) {
					if (!usedList.Contains(p) && p.AttackDamage != 0) {
						usedList.Add(p);
						if (DamageCounter<oHealth){
							DamageCounter = DamageCounter + p.AttackDamage;
							index ++;
							var mi = new MinionID(); //mi = minion id 
							mi.playerMinion = p;
							mi.opponentMinion = o;
							TurnTaskList.Add(mi);
							//Console.WriteLine($"The player minion is {p} and after {index} cards has done {DamageCounter} damage.");
						}
						else {
							continue;
						}
					}
				}
				//Console.WriteLine($"A total of {DamageCounter} damage has been done on {o}");
				continue;
			}
			//Console.WriteLine($"In the list of tasks for turn {game.Turn}, there are {TurnTaskList.Count} elements.");
			/*foreach (var x in usedList){
				Console.WriteLine($"Used Minions: {x}");
			}*/
		}

		private PlayerTask MinionAttack(List<PlayerTask> PlayerTaskList, POGame game){
			PlayerTask returnTask = null;
			/*playerList = GetMinions(game, game.CurrentPlayer);
			opponentList = GetMinions(game, game.CurrentOpponent);*/

			// There is no need to do anything if we have no minions
			if(playerList.Count == 0){
				//Console.WriteLine("Player has no minions");
				return returnTask;
			}

			List<PlayerTask> MinionTaskList;
		
			foreach(Minion m in playerList){
				
				//MinionTaskList = PlayerTaskList.Where(x => x.PlayerTaskType == PlayerTaskType.MINION_ATTACK && x.Source == m ).ToList();
				
				MinionTaskList = new List<PlayerTask>();

				foreach (PlayerTask task in PlayerTaskList){
					if(task.Source != null && task.Source.ToString() == m.ToString() && task.PlayerTaskType == PlayerTaskType.MINION_ATTACK){
						//Console.WriteLine($"MinionAttack Possible Task: {task}, Task Source: {task.Source}");	
						MinionTaskList.Add(task);
					}		
				}
				
				if(MinionTaskList.Count == 0){
					//most likely minion was only placed this turn and cannot attack
					//Console.WriteLine($"{m} has no tasks");
				}
				else if(MinionTaskList.Count == 1){
					//there is only one choice. When there is no opponent minions will attack hero					
					returnTask = MinionTaskList[0];
					//Console.WriteLine($"Return Minion only task: {returnTask}");
					return returnTask;
				}
				else{
					// multiple choices but just do first choice
					
					//OPTION 1: Attack by TaskTurnList
					if (TurnTaskList.Any() && turnTaskIndex < TurnTaskList.Count && turnTaskIndex > -1) {
						//Console.WriteLine($"There are {TurnTaskList.Count} elements in TurnTaskList list");
						//Console.WriteLine($"Turn task index {turnTaskIndex}");
						//Console.WriteLine($"There are {MinionTaskList.Count} elements in Minion Task List");
						int minionToAttack = 0;
						foreach (var x in MinionTaskList){
							//Console.WriteLine($"Minion Task List: {x}");
						}
						for (int i = 0; i < MinionTaskList.Count(); i++){
								//Console.WriteLine($"The task from the chosen list is {TurnTaskList[turnTaskIndex]}");
								//Console.WriteLine($"The matching task is {MinionTaskList[i]}");
								//Console.WriteLine($"player Minion = {TurnTaskList[turnTaskIndex].playerMinion.ToString()}");
								//Console.WriteLine($"Minion Task List {MinionTaskList[i].Source.ToString()}");
								//Console.WriteLine($"opponent Minion = {TurnTaskList[turnTaskIndex].opponentMinion.ToString()}");
								//Console.WriteLine($"Minion Task List {MinionTaskList[i].Target.ToString()}");
							if (TurnTaskList[turnTaskIndex].playerMinion.Id == MinionTaskList[i].Source.Id && TurnTaskList[turnTaskIndex].opponentMinion.Id == MinionTaskList[i].Target.Id){
								minionToAttack = i;
								//Console.WriteLine($"The index of minion to attack is {minionToAttack}");
								//Console.WriteLine($"This is turn task index before iteration: {turnTaskIndex}");
								turnTaskIndex++;
								//Console.WriteLine($"This is turn task index after iteration: {turnTaskIndex}");
							}
							else{
								continue;
							}
							break;
						}
						returnTask = MinionTaskList[minionToAttack];
					}
					
					//OPTION 2: Attack opponent hero by default
					else {
						int opponentHero = 0;
						//Console.WriteLine($"The opponent hero is {game.CurrentOpponent.Hero}");
						for (int i = 0; i < MinionTaskList.Count(); i++)	{
							//Console.WriteLine($"The target is {MinionTaskList[i].Target}");
							//Console.WriteLine($"Task at index {i} is {MinionTaskList[i]}");
							if (MinionTaskList[i].Target.ToString() == game.CurrentOpponent.Hero.ToString()){
								opponentHero = i;
								//Console.WriteLine($"The index of opponent hero is {opponentHero}");
								break;
							}
						}
						//Console.WriteLine($"This is index {opponentHero}");
						returnTask = MinionTaskList[opponentHero];
					}
					
					//Console.WriteLine($"Return Minion task from multiple tasks: {returnTask}");
					return returnTask;
				}
			}	
			/*if(returnTask == null){
				Console.WriteLine("Return Minion Attack task: No card selected");
			}
			else{
				Console.WriteLine($"Return Minion Attack task: {returnTask}");
			}*/
			return returnTask;
		}

		private PlayerTask heroAttack(List<PlayerTask> PlayerTaskList){
			PlayerTask returnTask = null;
			foreach (PlayerTask task in PlayerTaskList){
				if(task.PlayerTaskType == PlayerTaskType.HERO_ATTACK){
					//Console.WriteLine($"Hero Attack: {task}");
					returnTask = task;
				}
			}
			/*if(returnTask == null){
				Console.WriteLine("Return Hero Attack task: No card selected");
			}
			else{
				Console.WriteLine($"Return Hero Attack task: {returnTask}");
			}*/

			return returnTask;
		}

		private void updateStates(POGame game){
			// Code to track move number within a Turn
			if(game.Turn != currTurn){
				currTurn = game.Turn;
				moveNum = 1;
				turnTaskIndex = 0;
				TurnTasks(game, game.CurrentPlayer);
			}
			else{
				moveNum++;
			}
		}

		public void winGame(GameStats gamestats){
			gamestats.printResults();
		}

		public override PlayerTask GetMove(POGame game){
			var player = game.CurrentPlayer;
			//var opponent = game.CurrentOpponent;

			// Get all simulation results for simulations that didn't fail
			//var validOpts = game.Simulate(player.Options()).Where(x => x.Value != null); 
			var validOpts = game.Simulate(player.Options());

			//Put player options in a variable
			var playerOptions = player.Options();
			/*foreach (var x in playerOptions)
			{
				Console.WriteLine($"Here is an option {x.PlayerTaskType}");
			}*/

			List<PlayerTask> PlayerTaskList = new List<PlayerTask>(validOpts.Keys);
			
			PlayerTask endTurnTask = playerOptions.First();	
			PlayerTask returnTask;

			updateStates(game);

			//Prints the game turn and the remaining mana available
			//Console.WriteLine($"Turn {game.Turn} Move {moveNum}, you have {player.RemainingMana} mana available.");
			//Prints the board state for the game turn
			//Console.WriteLine($"Board State for Turn {game.Turn}:" + game.PartialPrint());

			InspectHand(player.HandZone);

			returnTask = PlayCard(PlayerTaskList, game.CurrentPlayer, player.HandZone, player.BoardZone, game);
			if(returnTask != null){	
				return returnTask;
			}

			returnTask = MinionAttack(PlayerTaskList, game);
			if(returnTask != null){
				return returnTask;
			}
			if (player.RemainingMana > 0){
				returnTask = HeroPower(PlayerTaskList, game.CurrentPlayer);
				if(returnTask != null){	
					return returnTask;
				}
			}

			returnTask = heroAttack(PlayerTaskList);
			if(returnTask != null){
				return returnTask;
			}

			//Console.WriteLine($"Turn {game.Turn}: Ending Turn {endTurnTask}");				
			return endTurnTask;
		}
	}

}

