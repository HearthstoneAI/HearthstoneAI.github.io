using SabberStoneBasicAI.Meta;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;


// TODO choose your own namespace by setting up <submission_tag>
// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.MAGEntMann
{
	class MAGEnt : AbstractAgent
	{
		private int pID;

		public MAGEnt()
		{
			preferedDeck = Decks.RenoKazakusMage;
			preferedHero = CardClass.MAGE;
		}

		private double getValue(POGame game)
		{
			if (game.CurrentPlayer.Id != pID)
			{
				return int.MinValue;
			}

			var player = game.CurrentPlayer;
			var opponent = game.CurrentOpponent;
			var p_board = player.BoardZone;
			var o_board = opponent.BoardZone;

			if (player.Hero.Health < 1)
			{
				return int.MinValue;
			}

			if (opponent.Hero.Health < 1)
			{
				return int.MaxValue;
			}

			int p_min_eff_atk = 0;
			int p_min_tot_atk = 0;
			int p_min_tot_hp = 0;
			int p_min_tau_hp = 0;

			p_board.ForEach(minion =>
			{
				int windfury = minion.HasWindfury ? 1 : 0;
				int taunt = minion.HasTaunt ? 1 : 0;
				int shield = minion.HasDivineShield ? 1 : 0;
				int can_attack = minion.CanAttack ? 1 : 0;

				p_min_eff_atk += minion.AttackDamage * can_attack + minion.AttackDamage * can_attack * windfury;
				p_min_eff_atk += minion.AttackDamage + minion.AttackDamage * windfury;
				p_min_tot_hp += minion.Health + minion.Health * shield;
				p_min_tau_hp += minion.Health * taunt + minion.Health * taunt * shield;
			});

			int o_min_eff_atk = 0;
			int o_min_tot_atk = 0;
			int o_min_tot_hp = 0;
			int o_min_tau_hp = 0;

			o_board.ForEach(minion =>
			{
				int windfury = minion.HasWindfury ? 1 : 0;
				int taunt = minion.HasTaunt ? 1 : 0;
				int shield = minion.HasDivineShield ? 1 : 0;
				int can_attack = minion.CanAttack ? 1 : 0;

				o_min_eff_atk += minion.AttackDamage * can_attack + minion.AttackDamage * can_attack * windfury;
				o_min_eff_atk += minion.AttackDamage + minion.AttackDamage * windfury;
				o_min_tot_hp += minion.Health + minion.Health * shield;
				o_min_tau_hp += minion.Health * taunt + minion.Health * taunt * shield;
			});


			double p_off_val = p_min_tot_atk * 0.5 + p_min_eff_atk + player.CurrentSpellPower + player.Hero.TotalAttackDamage;
			double p_def_val = p_min_tau_hp + p_min_tot_hp;
			double p_ctl_val = p_board.Count - o_board.Count;
			double o_off_val = o_min_tot_atk * 0.5 + o_min_eff_atk + o_min_eff_atk;
			double o_def_val = o_min_tau_hp + o_min_tot_hp;

			double my_value = p_off_val + p_def_val + p_ctl_val;
			double op_value = o_off_val + o_def_val;

			return my_value - op_value  + player.Hero.Health - opponent.Hero.Health;
		}


		public override PlayerTask GetMove(POGame game)
		{
			pID = game.CurrentPlayer.Id;
			var player = game.CurrentPlayer;
			var validOpts = game.Simulate(player.Options()).Where(x => x.Value != null);


			/* No look-ahead*/
			return validOpts.Any() ?
				validOpts.OrderBy(x => getValue(x.Value)).Last().Key :
				player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);

			/* with look-ahead */
			/*PlayerTask best_move = null;
			double best_value = 0;
			foreach (KeyValuePair<PlayerTask, POGame> pair in validOpts)
			{
				var game_ = pair.Value;
				var player_ = game_.CurrentPlayer;
				var move_ = pair.Key;

				if (player_.Id == player.Id)
				{
					var validOpts_ = game_.Simulate(player_.Options()).Where(x => x.Value != null);
					foreach (KeyValuePair<PlayerTask, POGame> pair_ in validOpts_)
					{
						double value = getValue(game, pair_.Value);
						if (value > best_value)
						{
							best_value = value;
							best_move = move_;
						}
					}
				}

			}
			if (best_move == null)
			{
				best_move = validOpts.OrderBy(x => getValue(game, x.Value)).Last().Key;
			}
			return best_move;*/
		}

		public override void InitializeAgent() { }
		public override void InitializeGame() { }
		public override void FinalizeGame() { }
		public override void FinalizeAgent() { }
	}
}
