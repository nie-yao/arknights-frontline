using System;
using ArknightsFrontline.Combat;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    /// <summary>
    /// Connects the match result, statistics, voting, and presentation components at runtime.
    /// </summary>
    public sealed class MatchPresentationBootstrap : MonoBehaviour
    {
        [SerializeField] private OperatorRosterController roster;
        [SerializeField] private MatchOutcomeController match;
        [SerializeField] private MatchStatisticsController statistics;
        [SerializeField] private TeamExitVoteController votes;
        [SerializeField] private MatchHudPresenter hud;
        [SerializeField] private CombatUnit blueTower;
        [SerializeField] private CombatUnit redTower;
        [SerializeField] private string playerStableKey;

        private Action exitAction;
        private bool hasStarted;
        private bool isInitialized;

        public void Configure(
            OperatorRosterController operatorRoster,
            MatchOutcomeController matchController,
            MatchStatisticsController statisticsController,
            TeamExitVoteController voteController,
            MatchHudPresenter hudPresenter,
            CombatUnit friendlyBlueTower,
            CombatUnit friendlyRedTower,
            string stableKey,
            Action exitAction = null)
        {
            roster = operatorRoster;
            match = matchController;
            statistics = statisticsController;
            votes = voteController;
            hud = hudPresenter;
            blueTower = friendlyBlueTower;
            redTower = friendlyRedTower;
            playerStableKey = stableKey;
            this.exitAction = exitAction;
            isInitialized = false;

            if (hasStarted)
            {
                InitializePresentation();
            }
        }

        private void Start()
        {
            hasStarted = true;
            InitializePresentation();
        }

        private void InitializePresentation()
        {
            var selection = GetComponent<PlayerCharacterSelection>();
            if (selection != null && selection.IsPending) return;
            if (isInitialized)
            {
                return;
            }

            if (roster == null || match == null || statistics == null || votes == null
                || hud == null || blueTower == null || redTower == null
                || string.IsNullOrWhiteSpace(playerStableKey))
            {
                throw new MissingReferenceException(
                    $"{nameof(MatchPresentationBootstrap)} requires roster, match, statistics, votes, HUD, "
                    + "both towers, and the player's stable roster key.");
            }

            // ArenaRosterBootstrap runs from Awake, so the roster already contains its six
            // stable seats and initial lives by the time this Start callback executes.
            statistics.Configure(match, roster, blueTower, redTower);
            votes.Configure(match, roster, autoApproveComputerVotes: true);
            hud.Configure(
                match,
                statistics,
                votes,
                blueTower,
                redTower,
                playerStableKey,
                exitAction);
            isInitialized = true;
        }

        private void Update()
        {
            if (hasStarted && !isInitialized) InitializePresentation();
        }
    }
}
