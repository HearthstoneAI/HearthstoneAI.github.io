using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.Score;
using SabberStoneBasicAI.PartialObservation;
using System.Diagnostics;
using SabberStoneBasicAI.Meta;
using SabberStoneCore.Model.Entities;

// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.BetaStone
{
	class BetaStone : AbstractAgent
	{

		public bool myDebug = false;
		public bool mcts = true;
		public int numSimulations = 100;


		//public static Dictionary<CardClass, List<Card>> classDeckDict = new Dictionary<CardClass, List<Card>>
		//{
		//	{ CardClass.WARRIOR, Decks.AggroPirateWarrior},
		//	{ CardClass.WARLOCK, Decks.ZooDiscardWarlock},
		//	{ CardClass.MAGE, Decks.RenoKazakusMage},
		//	{ CardClass.ROGUE, Decks.MiraclePirateRogue},
		//	{ CardClass.SHAMAN, Decks.MidrangeJadeShaman},
		//	{ CardClass.DRUID, Decks.MurlocDruid},
		//	{ CardClass.PALADIN, Decks.MidrangeBuffPaladin},
		//	{ CardClass.HUNTER, Decks.MidrangeSecretHunter},
		//	{ CardClass.PRIEST, Decks.RenoKazakusDragonPriest},
		//};

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

		public override PlayerTask GetMove(POGame poGame)
		{
			var player = poGame.CurrentPlayer;

			// Implement a simple Mulligan Rule
			if (player.MulliganState == Mulligan.INPUT)
			{
				List<int> mulligan = new ControlScore().MulliganRule().Invoke(player.Choice.Choices.Select(p => poGame.getGame().IdEntityDic[p]).ToList());
				return ChooseTask.Mulligan(player, mulligan);
			}

			// Apply MCTS and do the best move
			PlayerTask action = null;

			try
			{
				if (mcts)
				{
					action = MCTS(poGame.getCopy());
				}
				else
				{
					var legalMoves = poGame.Simulate(player.Options()).Where(x => x.Value != null);
					return legalMoves.Any() ?
						legalMoves.OrderBy(x => Score(x.Value, player.PlayerId)).Last().Key :
						player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);
				}
			}
			catch (NullReferenceException)
			{
				action = player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);
			}
			if (myDebug)
			{
				Console.WriteLine();
				Console.WriteLine(poGame.FullPrint());
				Console.WriteLine("Chose action: " + action);
			}
			return action;
		}

		public class Node
		{
			private Node parent;
			private KeyValuePair<PlayerTask, POGame> state;
			private List<Node> children; // List of child nodes
			private int t=0; // Accumulated rewards
			private int n=0; // Number of times this node has been visited
			private bool isEOT; // If the corresponding action is End Turn


			// Init
			public Node(Node parentNode, KeyValuePair<PlayerTask, POGame> gameState)
			{
				parent = parentNode;
				state = gameState;
				children = new List<Node>();
				isEOT = gameState.Key != null ?
					gameState.Key.PlayerTaskType == PlayerTaskType.END_TURN :
					false;
			}

			// Getters + Setters
			public Node Parent
			{
				get
				{
					return parent;
				}
			}

			public KeyValuePair<PlayerTask, POGame> State
			{
				get
				{
					return state;
				}
			}

			public List<Node> Children
			{
				get
				{
					return children;
				}
				set
				{
					children=value;
				}
			}

			public int T
			{
				get
				{
					return t;
				}
				set
				{
					t=value;
				}
			}

			public int N
			{
				get
				{
					return n;
				}
				set
				{
					n = value;
				}
			}

			public bool IsEOT
			{
				get { return isEOT; }
				set { isEOT = value; }
			}

		}


		public PlayerTask MCTS(POGame state)
		{
			Controller player = state.CurrentPlayer;
			var timer = Stopwatch.StartNew();
			Node rootNode = new Node(parentNode: null, gameState: new KeyValuePair<PlayerTask, POGame>(null, state));

			// Do as long as we've got time
			while (timer.ElapsedMilliseconds < 10000 && rootNode.N < numSimulations)
			{

				//Selection
				Node currentNode = Selection(rootNode);

				//Expansion
				currentNode = Expansion(currentNode, 2);

				//Rollout
				int result = Rollout(currentNode, player);

				//Backpropagation
				Backpropagate(currentNode, result);
			}

			// Return the action that has accumulated the highest rewards
			return rootNode.Children.Any() ?
				rootNode.Children.OrderBy(x => SafeCalculateValue(x.T, x.N)).Last().State.Key :
				rootNode.State.Value.CurrentPlayer.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);
		}

		public Node Selection(Node current)
		{
			if (current.Children.Count > 0)
			{
				// Get the Child that maximises UCB1
				current = current.Children.Select(x => UCB1(x)).OrderBy(x => x.Value).Last().Key;
				current = Selection(current);
			}
			return current;
		}

		public Node Expansion(Node current, int expandAfterNVisits)
		{
			List<PlayerTask> legalMoves = current.State.Value.CurrentPlayer.Options();
			if (current.N > expandAfterNVisits && current.Children.Count == 0 && !current.IsEOT)
			{

				var newStates = current.State.Value.Simulate(legalMoves).Where(x => x.Value != null).ToList();
				for (int i = 0; i < newStates.Count; i++)
				{
					current.Children.Add(new Node(parentNode: current, gameState: newStates[i]));
				}

				current = current.Children.Select(x => UCB1(x)).OrderBy(x => x.Value).Last().Key;
			}
			return current;
		}

		public int Rollout(Node node, Controller player)
		{

			POGame state = node.State.Value;
			KeyValuePair<PlayerTask, POGame> bestMove;
			//while (!(state.getGame().State == State.COMPLETE))
			do
			{
				List<PlayerTask> moves = state.CurrentPlayer.Options();
				var legalMoves = state.Simulate(moves).Where(x => x.Value != null).ToList();
				bestMove = legalMoves.OrderBy(x => Score(x.Value, state.CurrentPlayer.PlayerId)).Last();
				state = bestMove.Value;
			} while (bestMove.Key.PlayerTaskType != PlayerTaskType.END_TURN);

			int result = Reward(state, player.PlayerId, terminalIsEOT: true);

			return result;
		}

		public void Backpropagate(Node current, int reward)
		{
			// Backpropagate the reward back to the top
			while (!(current==null))
			{
				current.N++;
				current.T += reward;
				current = current.Parent;
			}
		}


		// UCB1 for deciding which node to follow
		public static KeyValuePair<Node, double> UCB1(Node current, int cSquared=2)  
		{
			double value;
			if (current.N == 0)
			{
				value = Double.MaxValue;
			}
			else
			{
				value = current.T / current.N + Math.Sqrt(cSquared * Math.Log(current.Parent.N) / current.N);
			}

			return new KeyValuePair<Node, double>(current, value);
		}

		public static int Reward(POGame state, int playerID, bool terminalIsEOT=false)
		{
			int reward;
			if (terminalIsEOT)
			{
				reward = Score(state, playerID);
			}
			else
			{
				reward = state.CurrentPlayer.PlayerId == playerID ?
					Convert.ToInt32(state.CurrentPlayer.PlayState == PlayState.WON) :
					Convert.ToInt32((state.CurrentPlayer.PlayState == PlayState.CONCEDED) || (state.CurrentPlayer.PlayState == PlayState.LOST));
			}
			return reward;
		}

		// Replace unknown cards with cards randomly drawn from the controller's hero class for meaningful simulations
		// Doesn't matter cause opponents turn are not accessible anyway
		//public static void ReplaceNoWay(POGame game)
		//{
		//	List<Card> cardPool = classDeckDict[game.CurrentPlayer.HeroClass];

		//	SabberStoneCore.Model.Zones.HandZone playerHand = game.CurrentPlayer.HandZone;
		//	for (int i = 0; i < playerHand.Count; i++)
		//	{
		//		if (playerHand[i].Card == Cards.FromId("LOEA04_31b"))
		//		{
		//			playerHand[i].Card = cardPool[new Random().Next(cardPool.Count)];
		//		}
		//	}
		//}

		private static int Score(POGame state, int playerId)
		{
			var p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;
			switch (state.CurrentPlayer.HeroClass)
			{ 
				case CardClass.WARRIOR: return new AggroScore { Controller = p }.Rate();
				case CardClass.WARLOCK: return new RampScore { Controller = p }.Rate();
				case CardClass.HUNTER:  return new MidRangeScore { Controller = p }.Rate();
				case CardClass.SHAMAN:  return new MidRangeScore { Controller = p }.Rate();
				case CardClass.PALADIN: return new MidRangeScore { Controller = p }.Rate();
				case CardClass.DRUID:   return new MidRangeScore { Controller = p }.Rate();
				case CardClass.ROGUE:   return new RampScore { Controller = p }.Rate();
				case CardClass.MAGE:    return new ControlScore { Controller = p }.Rate();
				case CardClass.PRIEST:  return new RampScore { Controller = p }.Rate();
				default: return new RampScore { Controller = p }.Rate();
			}
		} 

		// For calculation of value of nodes should they have not been visited
		public static double SafeCalculateValue(int t, int n)
		{
			return n != 0 ?
				t / n :
				0.0;
		}
	}
}
