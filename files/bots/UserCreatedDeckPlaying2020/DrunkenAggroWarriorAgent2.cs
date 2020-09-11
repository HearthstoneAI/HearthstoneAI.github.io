using System;
using System.Linq;
using SabberStoneBasicAI.Meta;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using System.ComponentModel;

namespace SabberStoneBasicAI.AIAgents.FinalAgent
{

    class PlayerWeights
    {
        public double HeroHealth = 0;
        public double HeroAttackDamage = 0;
        public double TauntCount = 0;
        public double TauntHealth = 0;
        public double TauntAttackDamage = 0;
        public double MinionCount = 0;
        public double MinionHealth = 0;
        public double MinionAttackDamage = 0;
    }

    class Weights
    {
        public PlayerWeights Me = new PlayerWeights();
        public PlayerWeights Opponent = new PlayerWeights();
    }

    class AggroWeights : Weights
    {
        public AggroWeights()
        {
            Me.HeroAttackDamage = 98;
            Me.HeroHealth = 49;
            Me.MinionCount = 68;
            Me.MinionAttackDamage = 40;
            Me.MinionHealth = 26;
            Me.TauntCount = 57;
            Me.TauntAttackDamage = 41;
            Me.TauntHealth = 2;

            Opponent.HeroAttackDamage = 37;
            Opponent.HeroHealth = 3;
            Opponent.MinionCount = 11;
            Opponent.MinionAttackDamage = 93;
            Opponent.MinionHealth = 35;
            Opponent.TauntCount = 52;
            Opponent.TauntAttackDamage = 69;
            Opponent.TauntHealth = 34;
        }
    }
    class DrunkenAggroWarriorAgent2 : AbstractAgent
    {

        private Weights Weights = new AggroWeights();

		public DrunkenAggroWarriorAgent2()
		{
			preferedHero = CardClass.WARRIOR;
			preferedDeck = Decks.AggroPirateWarrior;
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

        private PlayerTask HandleMulligan(POGame poGame)
        {
            Controller me = poGame.CurrentPlayer;
            if (me.MulliganState == Mulligan.INPUT)
            {
                List<IPlayable> cards = me.Choice.Choices.Select(p => poGame.getGame().IdEntityDic[p]).ToList();
                return ChooseTask.Mulligan(me, cards.Where(x => x.Card.Cost <= 3).Select(x => x.Id).ToList());
            }

            return null;
        }

        private double GetHeroPowerScore(CardClass? cardClass)
        {
            switch (cardClass)
            {
                case CardClass.HUNTER:
                    return 10;
                case CardClass.ROGUE:
                    return 9;
                case CardClass.DRUID:
                    return 8;
                case CardClass.MAGE:
                    return 7;
                case CardClass.SHAMAN:
                    return 6;
                case CardClass.PALADIN:
                    return 5;
                case CardClass.WARRIOR:
                    return 4;
                default:
                    return 0;
            }
        }

        private double ComputePlayerScore(Controller player, PlayerWeights weights)
        {
            // Hero
            double score = weights.HeroHealth * player.Hero.Health;
            score += weights.HeroAttackDamage * player.Hero.TotalAttackDamage;

            // Minions
            int minionCount = 0;
            int minionAttackDamage = 0;
            int minionHealth = 0;

            int tauntCount = 0;
            int tauntAttackDamage = 0;
            int tauntHealth = 0;

            player.BoardZone.ForEach(m =>
            {
                if (m.HasTaunt)
                {
                    tauntCount++;
                    tauntAttackDamage += m.AttackDamage;
                    tauntHealth += m.Health;
                }
                else
                {
                    minionCount++;
                    minionAttackDamage += m.AttackDamage;
                    minionHealth += m.Health;
                }
            });

            score += weights.MinionCount * minionCount;
            score += weights.MinionAttackDamage * minionAttackDamage;
            score += weights.MinionHealth * minionHealth;

            score += weights.TauntCount * tauntCount;
            score += weights.TauntAttackDamage * tauntAttackDamage;
            score += weights.TauntHealth * tauntHealth;

            return score;
        }

        private double ComputeScore(POGame poGame, int playerId)
        {
            Controller opponent = playerId == poGame.CurrentPlayer.PlayerId ? poGame.CurrentOpponent : poGame.CurrentPlayer;
            if (opponent.Hero.Health <= 0)
            {
                return Int64.MaxValue;
            }

            Controller me = playerId == poGame.CurrentPlayer.PlayerId ? poGame.CurrentPlayer : poGame.CurrentOpponent;
            if (me.Hero.Health <= 0)
            {
                return Int64.MinValue;
            }

            double score = GetHeroPowerScore(me.Hero.HeroPower.Card.Class);
            score += ComputePlayerScore(me, Weights.Me);
            score -= ComputePlayerScore(opponent, Weights.Opponent);
            return score;
        }

        // Simulates an option and returns the score that this option achieves
        // maxDepth: the maximum amount of recursive calls that is allowed
        private KeyValuePair<PlayerTask, double> Simulate(POGame poGame, int playerId, int maxDepth = 3)
        {
            List<PlayerTask> options = poGame.CurrentPlayer.Options();

            Dictionary<PlayerTask, double> scores = new Dictionary<PlayerTask, double>();
            options.ForEach(o => scores[o] = 0.0);

            poGame.Simulate(options).ToList().ForEach(simulation =>
            {
                if (simulation.Value == null)
                {
                    scores[simulation.Key] = Double.MinValue;
                    return;
                }

                scores[simulation.Key] += ComputeScore(simulation.Value, playerId);
                if (maxDepth > 0 && playerId == poGame.CurrentPlayer.PlayerId)
                {
                    scores[simulation.Key] += Simulate(simulation.Value, playerId, maxDepth - 1).Value;
                }
            });

            // Get the option with the highest score
            return scores.Aggregate((x, y) => x.Value > y.Value ? x : y);
        }

        private PlayerTask GetBestOption(POGame poGame)
        {
            int playerId = poGame.CurrentPlayer.PlayerId;
            List<PlayerTask> options = poGame.CurrentPlayer.Options();
            int optionCount = options.Count;
            int maxDepth = optionCount > 20 ? 1 : optionCount > 10 ? 2 : 3;

            return Simulate(poGame, playerId, maxDepth).Key;
        }

        public override PlayerTask GetMove(POGame poGame)
        {

            // Handle mulligan input
            PlayerTask action = HandleMulligan(poGame);
            if (action != null)
            {
                return action;
            }

            var option = GetBestOption(poGame);
            return option;
        }
    }
}
