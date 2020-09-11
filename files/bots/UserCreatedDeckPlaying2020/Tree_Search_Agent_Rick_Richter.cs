using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SabberStoneBasicAI.Meta;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneBasicAI.Score;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneBasicAI.AIAgents.Richter
{
	class Tree_Search_Agent_Rick_Richter : AbstractAgent
	{
		public Tree_Search_Agent_Rick_Richter()
		{
			preferedDeck = Decks.MidrangeJadeShaman; //defualt value if no deck is provided
			preferedHero = CardClass.SHAMAN; //default value
		}

		class TreeNode
		{
			public TreeNode()
			{
				action_state = new KeyValuePair<PlayerTask, POGame>();
				score = int.MinValue;
				childs = new List<TreeNode>();
			}
			public TreeNode(KeyValuePair<PlayerTask, POGame> _action_state, int _score)
			{
				action_state = _action_state;
				score = _score;
				childs = new List<TreeNode>();
			}

			public KeyValuePair<PlayerTask, POGame> action_state;
			public int score;
			public List<TreeNode> childs;
		}

		class Tree
		{
			public Tree(TreeNode node)
			{
				root = node;
			}
			public Tree(POGame state)
			{
				root = new TreeNode(new KeyValuePair<PlayerTask, POGame>(null, state), int.MinValue);
			}
			public TreeNode root;

			public PlayerTask Get_best()
			{
				List<KeyValuePair<int, TreeNode>> l = new List<KeyValuePair<int, TreeNode>>();
				foreach (var child in root.childs)
				{
					l.Add(new KeyValuePair<int, TreeNode>(Max_Score_in_Path(child), child));
				}
				return l.OrderBy(x => x.Key).Last().Value.action_state.Key;
			}

			private int Max_Score_in_Path(TreeNode node)
			{
				int maximum_score = node.score;
				if (node.childs.Count() != 0)
				{
					foreach (var child in node.childs)
					{
						maximum_score = Math.Max(maximum_score, Max_Score_in_Path(child));
					}
				}
				return maximum_score;
			}
		}

		private static void Build_backup_diagram(TreeNode _root, int depth, Controller p)
		{
			var player = _root.action_state.Value.CurrentPlayer;
			var validOpts = _root.action_state.Value.Simulate(player.Options()).Where(x => x.Value != null);

			if (validOpts.Count() >= 5)
			{
				depth--;
				if (validOpts.Count() >= 25)
				{
					depth--;
				}
			}
			if (depth >= 0 && _root.action_state.Value.CurrentPlayer.PlayerId == p.PlayerId)
			{
				_root.childs = validOpts.Select(x => new TreeNode(x, Score(x.Value, player.PlayerId))).ToList();

				foreach (var node in _root.childs)
				{
					Build_backup_diagram(node, depth - 1, p);
				}
			}
		}

		public override void FinalizeAgent()
		{
		}

		public override void FinalizeGame()
		{
		}

		public override void InitializeAgent()
		{
		}

		public override void InitializeGame()
		{
		}

		public override PlayerTask GetMove(POGame poGame)
		{
			Tree search_tree = new Tree(poGame);
			Build_backup_diagram(search_tree.root, 3, poGame.CurrentPlayer);
			var best = search_tree.Get_best();
			return best;
		}

		private static int Score(POGame state, int playerId)
		{
			var p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;
			switch (state.CurrentPlayer.HeroClass)
			{
				case CardClass.WARRIOR: return new AggroScore { Controller = p }.Rate();
				case CardClass.MAGE: return new ControlScore { Controller = p }.Rate();
				default: return new RampScore { Controller = p }.Rate();
			}			
		}
	}
}
