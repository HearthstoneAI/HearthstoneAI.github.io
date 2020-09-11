using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SabberStoneBasicAI.AIAgents.MCTSHans
{

	class AgentHansUCB1 : AgentHans
	{
		public override void setOptions()
		{
			UCB1_C = 500.0;
			TREE_POLICY = "ucb1";
			AGENT_NAME = "AgentHansUCB1";
		}
	}


	class AgentHansEGREEDY7 : AgentHans
	{
		public override void setOptions()
		{
			EPS = 0.7;
			TREE_POLICY = "egreedy";
			AGENT_NAME = "AgentHansEGREEDY7";
		}
	}

	class AgentHansEGREEDY5 : AgentHans
	{
		public override void setOptions()
		{
			EPS = 0.5;
			TREE_POLICY = "egreedy";
			AGENT_NAME = "AgentHansEGREEDY5";
		}
	}

	class AgentHansEGREEDY3: AgentHans
	{
		public override void setOptions()
		{
			EPS = 0.3;
			TREE_POLICY = "egreedy";
			AGENT_NAME = "AgentHansEGREEDY3";
		}
	}


	class AgentHansRANDOM : AgentHans
	{
		public override void setOptions()
		{
			TREE_POLICY = "random";
			AGENT_NAME = "AgentHansRANDOM";
		}
	}


	class AgentHansDECAY1 : AgentHans
	{
		public override void setOptions()
		{
			TREE_POLICY = "decayegreedy";
			AGENT_NAME = "AgentHansDECAY1";
		}

		public override void decay(Stopwatch sw, int maxMilliseconds)
		{
			EPS = Math.Exp(-TimeSpan.FromTicks(sw.Elapsed.Ticks).TotalMilliseconds / maxMilliseconds);
		}
	}

	class AgentHansDECAY2 : AgentHans
	{
		public override void setOptions()
		{
			TREE_POLICY = "decayegreedy";
			AGENT_NAME = "AgentHansDECAY2";
		}

		public override void decay(Stopwatch sw, int maxMilliseconds)
		{
			EPS = Math.Exp(-TimeSpan.FromTicks(sw.Elapsed.Ticks).TotalMilliseconds / (maxMilliseconds / 2));
		}
	}

	class AgentHansDECAY3 : AgentHans
	{
		public override void setOptions()
		{
			TREE_POLICY = "decayegreedy";
			AGENT_NAME = "AgentHansDECAY3";
		}

		public override void decay(Stopwatch sw, int maxMilliseconds)
		{
			EPS = Math.Exp(-TimeSpan.FromTicks(sw.Elapsed.Ticks).TotalMilliseconds / (maxMilliseconds / 3));
		}
	}

	class AgentHansDECAY4 : AgentHans
	{
		public override void setOptions()
		{
			TREE_POLICY = "decayegreedy";
			AGENT_NAME = "AgentHansDECAY4";
		}

		public override void decay(Stopwatch sw, int maxMilliseconds)
		{
			EPS = Math.Exp(-TimeSpan.FromTicks(sw.Elapsed.Ticks).TotalMilliseconds / (maxMilliseconds / 4));
		}
	}


	abstract class AgentHans : AbstractAgent
	{
		private int maxVisitedNum = Int32.MinValue;
		private int minVisitedNum = Int32.MaxValue;
		private int totalVisitedNum = 0;
		private int numMoves = 0;
		private Random rnd = new Random();
		public double EPS = 0.5;
		public int MAX_SIMULATION_DEPTH = 5;
		public double UCB1_C = 500.0;
		public string TREE_POLICY;
		public string AGENT_NAME = "default";

		public override PlayerTask GetMove(POGame poGame)
		{
			Controller player = poGame.CurrentPlayer;
			IEnumerable<KeyValuePair<PlayerTask, POGame>> validOpts = poGame.Simulate(player.Options()).Where(x => x.Value != null);
			int optcount = validOpts.Count();
			int maxMilliseconds = 25000; // Math.Min(Math.Max(optcount * 2000, 8000), 20000);
			// Implement a simple Mulligan Rule
			if (player.MulliganState == Mulligan.INPUT)
			{
				List<int> mulligan = new MyScore().MulliganRule().Invoke(player.Choice.Choices.Select(p => poGame.getGame().IdEntityDic[p]).ToList());
				return ChooseTask.Mulligan(player, mulligan);
			}
			PlayerTask returnTask = validOpts.Any() ? GetMoveMCTS(poGame.getCopy(), maxMilliseconds) : player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);
			if(returnTask == null) returnTask = player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);
			return returnTask;
		}

		public PlayerTask GetMoveMCTS(POGame poGame, int maxMilliseconds)
		{
			MCTSNode root = new MCTSNode(poGame);
			int numSimulation = 0;
			Stopwatch sw = new Stopwatch();
			sw.Start();
			//stop if confidence in the best node is high enough
			while(TimeSpan.FromTicks(sw.Elapsed.Ticks).TotalMilliseconds < maxMilliseconds)
			{
				if (TREE_POLICY == "decayegreedy") decay(sw, maxMilliseconds);
				MCTSNode node = Selection(root);
				MCTSNode expandedNode = Expansion(ref node);
				int numRepeat = 1;
				if(expandedNode.poGame.CurrentPlayer.PlayerId != poGame.CurrentPlayer.PlayerId)
				{
					//numRepeat = poGame.CurrentOpponent.DeckZone.Count;
					numRepeat = 4;
				}

				for (int i = 0; i < numRepeat; i++)
				{
					POGame simGame = Simulation(expandedNode, poGame.CurrentPlayer.PlayerId);
					Backpropagation(node, Score(simGame, poGame.CurrentPlayer.PlayerId));
				}
				numSimulation++;
			}
			if(root.visitsNum <= 1) Console.WriteLine(sw.ElapsedMilliseconds);
			maxVisitedNum = maxVisitedNum > root.visitsNum ? maxVisitedNum : root.visitsNum;
			minVisitedNum = minVisitedNum < root.visitsNum ? minVisitedNum : root.visitsNum;
			totalVisitedNum += root.visitsNum;
			numMoves++;

			root.children.Sort((a, b) => a.value.CompareTo(b.value));
			MCTSNode chosenNode = Policy(root, "greedy");
			if (chosenNode == root) return null;
			return chosenNode.task;
		}

		public MCTSNode Selection(MCTSNode node)
		{
			if (node.children.Count == 0)
			{
				return node;
			}
			MCTSNode selectedNode = Policy(node, TREE_POLICY);
			return Selection(selectedNode);
		}

		public MCTSNode Expansion(ref MCTSNode node)
		{
			//create all child nodes and select one
			IEnumerable<KeyValuePair<PlayerTask, POGame>> subactions = node.poGame.Simulate(node.poGame.CurrentPlayer.Options()).Where(x => x.Value != null);

			foreach (KeyValuePair<PlayerTask, POGame> subact in subactions)
			{
				MCTSNode newNode = new MCTSNode(node, subact.Key, subact.Value.getCopy());
				node.children.Add(newNode);

			}
			return Policy(node, "random");
		}

		public POGame Simulation(MCTSNode node, int myPlayerId)
		{
			int depth = MAX_SIMULATION_DEPTH;
			POGame simGame = node.poGame.getCopy();
			//if(node.poGame.CurrentPlayer.Id != myPlayerId) simGame = createOpponentHand(simGame.getCopy());
			while (depth > 0)
			{
				IEnumerable<KeyValuePair<PlayerTask, POGame>> subactions = simGame.Simulate(simGame.CurrentPlayer.Options()).Where(x => x.Value != null);
				if (!subactions.Any()) break;
				simGame = subactions.RandomElement(rnd).Value.getCopy();
				depth--;
			}
			return simGame;
		}

		public void Backpropagation(MCTSNode node, float value)
		{
			node.visitsNum++;
			node.valuesSum += value;
			node.value = node.valuesSum / node.visitsNum;
			if (node.children.Count > 0)
			{
				foreach (MCTSNode child in node.children)
				{
					child.ucb1 = UCB1(child);
				}
				if (TREE_POLICY == "ucb1") node.children.Sort((a, b) => a.ucb1.CompareTo(b.ucb1));
				else node.children.Sort((a, b) => a.value.CompareTo(b.value));
			}
			if (node.parent != null)
			{
				Backpropagation(node.parent, value);
			}
		}

		public double UCB1(MCTSNode node)
		{
			return node.parent != null ? node.value + UCB1_C * Math.Sqrt(2.0 * Math.Log(node.parent.visitsNum) / (node.visitsNum + 1.0)) : 0;
		}

		public virtual void decay(Stopwatch sw, int maxMilliseconds)
		{
			EPS = Math.Exp(-TimeSpan.FromTicks(sw.Elapsed.Ticks).TotalMilliseconds / maxMilliseconds);
		}


		public MCTSNode Policy(MCTSNode node, string policy="default")
		{
			switch (policy)
			{
				case "egreedy":
					if (rnd.NextDouble() >= EPS) return Policy(node, "greedy");
					else return Policy(node, "random");

				case "ucb1":
				case "greedy":
					//children are ordered by the backpropagation
					if (node.children.Count == 0)
					{
						return node;
					}
					return node.children[node.children.Count - 1];

				case "random":
				default:
					//random
					if (node.children.Count == 0)
					{
						return node;
					}
					return node.children[rnd.Next(node.children.Count)];
			}
		}


		private POGame createOpponentHand(POGame game)
		{
			int cardsToAdd = game.CurrentPlayer.HandZone.Count;

			List<Card> deckCards = game.CurrentPlayer.Standard.Where(x => remainInDeck(x, game.CurrentPlayer)).ToList();
			game.CurrentPlayer.HandZone = new HandZone(game.CurrentPlayer);
			while (cardsToAdd > 0 && deckCards.Count > 0 && !game.CurrentPlayer.HandZone.IsFull)
			{
				Card card = deckCards.RandomElement(new Random());
				//game.addCardToHandZone(card, game.CurrentPlayer);
				cardsToAdd--;
				deckCards = deckCards.Where(x => remainInDeck(x, game.CurrentPlayer)).ToList();
			}
			return game;
		}

		private bool remainInDeck(Card card, Controller player)
		{
			int occurences = player.HandZone.Count(c => c.Card == card) + player.GraveyardZone.Count(c => c.Card == card) + player.BoardZone.Count(c => c.Card == card);
			return card.Class == player.HeroClass && card.Cost <= player.RemainingMana && occurences < card.MaxAllowedInDeck;
		}


		public override void FinalizeAgent()
		{
		}

		public override void InitializeAgent()
		{
			setOptions();
		}

		public override void InitializeGame() { }
		public override void FinalizeGame()
		{
			//Console.WriteLine(" Avg. Simulations: " + totalVisitedNum / numMoves);
			//Console.WriteLine(" Max. Simulations: " + maxVisitedNum);
			//Console.WriteLine(" Min. Simulations: " + minVisitedNum);
		}

		public abstract void setOptions();

		private int Score(POGame state, int playerId)
		{
			Controller p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;
			return new MyScore { Controller = p }.Rate();
		}
	}


	class MCTSNode
	{

		public MCTSNode parent { get; set; }

		public PlayerTask task { get; set; }

		public POGame poGame { get; set; }

		public List<MCTSNode> children;

		public double valuesSum { get; set; }
		public int visitsNum { get; set; }

		public double value { get; set; }

		public double ucb1 { get; set; }


		public MCTSNode(POGame poGame)
		{
			parent = null;
			task = null;
			children = new List<MCTSNode>();
			this.poGame = poGame;
		}

		public MCTSNode(MCTSNode parent, PlayerTask task, POGame poGame)
		{
			this.parent = parent;
			this.task = task;
			this.poGame = poGame;
			children = new List<MCTSNode>();
		}

	}


	public class MyScore : Score.Score
	{
		public override int Rate()
		{
			if (OpHeroHp < 1)
				return Int32.MaxValue;

			if (HeroHp < 1)
				return Int32.MinValue;

			int result = 0;

			if (OpMinionTotHealthTaunt > 0)
				result += OpMinionTotHealthTaunt * -100;

			if (OpBoardZone.Count == 0 && BoardZone.Count > 0)
				result += 1000;

			if (OpMinionTotHealthTaunt > 0)
				result += MinionTotHealthTaunt * -500;

			result += (BoardZone.Count - OpBoardZone.Count) * 10;

			result += MinionTotAtk * 10;

			result += (HeroHp - OpHeroHp) * 10;

			result += (MinionTotHealth - OpMinionTotHealth) * 10;

			result += (MinionTotAtk - OpMinionTotAtk) * 10;

			result += (MinionTotHealthTaunt - OpMinionTotHealthTaunt) * 25;

			result += (BoardZone.Count - OpBoardZone.Count) * 5;

			//result += Controller.RemainingMana * 5;

			return result;
		}

		public override Func<List<IPlayable>, List<int>> MulliganRule()
		{
			return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
		}
	}


}
