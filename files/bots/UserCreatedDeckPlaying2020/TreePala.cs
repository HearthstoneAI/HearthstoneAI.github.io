using System;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneBasicAI.Meta;
using System.Diagnostics;
using SabberStoneBasicAI.Score;
using System.Linq;
using System.Net;
using System.Collections.Specialized;
using SabberStoneCore.Model.Entities;


//Bot by Philip Berndt and Michael Albrecht

// TODO choose your own namespace by setting up <submission_tag>
// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.Clearvoyant_Paladin
{
    class Node
    {
        public double alpha = 0.1;
        public double Q = 0;
        public int visits = 0;
        public List<Node> childNodes = new List<Node>();
        public PlayerTask nodeAction = null;
        public POGame state = null;
        public Node parent = null;
    }

    class TreePala : AbstractAgent
    {
        

        private MCTS mcts = null;
        private int lastTurnCounter = 0;
        private List<PlayerTask> actionsToDo = new List<PlayerTask>();
        private Random Rnd = new Random();

		public TreePala()
		{
			preferedDeck = Decks.MidrangeBuffPaladin;
			preferedHero = CardClass.PALADIN;
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

        public override void InitializeGame()
        {
        }

        public override PlayerTask GetMove(POGame poGame)
        {
            if (poGame.Turn != lastTurnCounter)
            {
				//Console.WriteLine("New turn " + poGame.Turn + "\n");
                actionsToDo.Clear();
                lastTurnCounter = poGame.Turn;

                mcts = new MCTS(poGame);
                actionsToDo = mcts.run();
            }

            var player = poGame.CurrentPlayer;

			/*
            Console.WriteLine("------------------------------------" + "\nCards on hand:\n" + String.Join(";\n", player.HandZone));
            Console.WriteLine("\nCurrent Turn and Mana:\n" + poGame.Turn + ";\n" + player.RemainingMana);
            Console.WriteLine("\nCurrent Health and Enemy Health:\n" + player.Hero.Health + ";\n" + player.Opponent.Hero.Health);

            Console.WriteLine("\nCards on enemy field:\n" + String.Join(";\n", player.Opponent.BoardZone));
            if (player.Opponent.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health) > 0)
                Console.WriteLine("Total taunt on enemy side:\n" + player.Opponent.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health));

            Console.WriteLine("\nCards on own field:\n" + String.Join(";\n", player.BoardZone));
            if (player.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health) > 0)
                Console.WriteLine("Total taunt on own side:\n" + player.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health));
			*/

			if (actionsToDo.Any()) {
				PlayerTask task = actionsToDo[0];
				actionsToDo.RemoveAt(0);
				return task;
			} else {
				return poGame.CurrentPlayer.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);
			}



            /*

			Root = new Node();

			var player = poGame.CurrentPlayer;

			// simple Mulligan Rule
			if (player.MulliganState == Mulligan.INPUT)
			{
				List<int> mulligan = new MidRangeScore().MulliganRule().Invoke(player.Choice.Choices.Select(p => poGame.getGame().IdEntityDic[p]).ToList());
				return ChooseTask.Mulligan(player, mulligan);
			}

			// Get all valid options for this turn
			var validOpts = poGame.Simulate(player.Options()).Where(x => x.Value != null);
			validOpts.OrderBy(x => new MidRangeScore { Controller = player }.Rate());

			// Build initial tree
			foreach (KeyValuePair<PlayerTask, POGame> actionState in validOpts)
			{
				var appnode = new Node();
				appnode.state = actionState.Value;
				appnode.nodeAction = actionState.Key;
				appnode.Q = new MidRangeScore { Controller = player }.Rate(); //might be wrong
				Root.childNodes.Append<Node>(appnode);
			}

			// Stats output
			List<PlayerTask> options = new List<PlayerTask>();
			if (validOpts.Any())
			{
				Console.WriteLine("------------------------------------" + "\nCards on hand:\n" + String.Join(";\n", player.HandZone));
				Console.WriteLine("\nCurrent Turn and Mana:\n" + poGame.Turn + ";\n" + player.RemainingMana);
				Console.WriteLine("\nCurrent Health and Enemy Health:\n" + player.Hero.Health + ";\n" + player.Opponent.Hero.Health);

				Console.WriteLine("\nCards on enemy field:\n" + String.Join(";\n", player.Opponent.BoardZone));
				if (player.Opponent.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health) > 0)
					Console.WriteLine("Total taunt on enemy side:\n" + player.Opponent.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health));

				Console.WriteLine("\nCards on own field:\n" + String.Join(";\n", player.BoardZone));
				if (player.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health) > 0)
					Console.WriteLine("Total taunt on own side:\n" + player.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health));

				options = poGame.CurrentPlayer.Options();

			}
			return options[Rnd.Next(options.Count)];
			*/
        }

        class MCTS
        {
            public int allVisits = 0;
            public Node rootNode = new Node();
            private Stopwatch _watch = new Stopwatch();
            private Random Rnd = new Random();

            private int MAX_TURN_TIME_MS = 5 * 1000;
            private double C = 1.4;

            public MCTS(POGame poGame)
            {
                _watch.Start();

                rootNode.state = poGame;

                expansion(rootNode);
				if (rootNode.childNodes.Count > 1) {
					for(int i = rootNode.childNodes.Count-1;i>=0;--i) {
						if (rootNode.childNodes[i].nodeAction.PlayerTaskType == PlayerTaskType.END_TURN) {
							rootNode.childNodes.RemoveAt(i);
							break;
						}
					}
				}

            }

            public List<PlayerTask> run()
            {
                Node selected = null;
				int panicCounter = 0;
                
					
                while (true)
                {
					panicCounter+=1;
					if (panicCounter > 100000) {
						//Console.Write("panic!!!!! run\n");
						break;
					}
                    selected = selection(rootNode);
                    expansion(selected);

					if (!selected.childNodes.Any()) {
						break;
					}
					Node randomChild = selected.childNodes[Rnd.Next(selected.childNodes.Count)];

                    double reward = simulation(randomChild);
                    backpropagation(selected, reward);

                    if (_watch.ElapsedMilliseconds >= MAX_TURN_TIME_MS)
                    {
                        break;
                    }
                }

                List<PlayerTask> list = new List<PlayerTask>();
                selected = selection(rootNode);

				panicCounter = 0;
                while (selected != null)
                {
					panicCounter+=1;
					if (panicCounter > 20) {
						//Console.Write("panic!!!!! run2\n");
						break;
					}

                    if (selected.parent == null)
                    {
                        break;
                    }
                    list.Add(selected.nodeAction);
                    selected = selected.parent;
                }
                list.Reverse();
				try {
                	list.Add(rootNode.state.CurrentPlayer.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN));
				} catch(InvalidOperationException e) {
					//Console.WriteLine("AAAAA");
				}
                return list;
            }

            public Node selection(Node root)
            {
                Node reNode = root;
                
				int panic = 0;
                while (reNode.childNodes.Count > 0)
                {
					panic+=1;
					if (panic > 1000) {
						//Console.Write("panic!!!!! select\n");
						break;
					}
					double maxScore = double.MinValue;
                    foreach (Node select in reNode.childNodes)
                    {
                        if (UctValue(select) > maxScore)
                        {
                            maxScore = UctValue(select);
                            reNode = select;
                        }
                    }
                }

                return reNode;
            }

            private double UctValue(Node node)
            {
                if (node.visits == 0)
                    return node.Q;
                return node.Q + C * Math.Sqrt(Math.Log(allVisits, 2) / node.visits);
            }

            public void expansion(Node node)
            {
                if (!node.childNodes.Any())
                {
                    var possibleActions = node.state.Simulate(node.state.CurrentPlayer.Options()).Where(x => x.Value != null);				
				
					foreach (KeyValuePair<PlayerTask, POGame> actionState in possibleActions)
					{
						if (actionState.Key.PlayerTaskType == PlayerTaskType.END_TURN) {
							continue;
						}
						Node appnode = new Node();
						appnode.state = actionState.Value;
						appnode.nodeAction = actionState.Key;
						appnode.parent = node;
						MidRangeScore rater = new MidRangeScore { Controller = actionState.Value.CurrentPlayer };
						appnode.Q = rater.Rate();
						node.childNodes.Add(appnode);
					}
                }
            }

            public double simulation(Node node)
            {	
                var validOpts = node.state.Simulate(node.state.CurrentPlayer.Options()).Where(x => x.Value != null);
                var choosenAction = validOpts.RandomElement(Rnd);
                int reward = 0;

				int panic = 0;
                while (choosenAction.Key.PlayerTaskType != PlayerTaskType.END_TURN && reward != int.MaxValue && reward != int.MinValue)
                {
					panic +=1;
					if (panic >= 100){
						//Console.Write("panic!!!!! sim\n");
						break;
					}
                    validOpts = choosenAction.Value.Simulate(choosenAction.Value.CurrentPlayer.Options()).Where(x => x.Value != null);
                    choosenAction = validOpts.RandomElement(Rnd);

                    MidRangeScore rater = new MidRangeScore { Controller = choosenAction.Value.CurrentPlayer };
                    reward = rater.Rate();

                    if (_watch.ElapsedMilliseconds >= MAX_TURN_TIME_MS)
                    {
                        break;
                    }
                }

                return (double)reward;
            }

            public void backpropagation(Node exploreNode, double reward)
            {
                Node tempNode = exploreNode;
				int panicCounter = 0;
                while (tempNode != null)
                {
					panicCounter+=1;
					if (panicCounter > 100) {
						//Console.Write("panic!!!!! propagate\n");
						break;
					}

                    tempNode.visits += 1;
                    tempNode.Q += (tempNode.alpha - tempNode.Q) * reward;
                    tempNode = tempNode.parent;
                }
                allVisits += 1;
            }
        }
    }
}
