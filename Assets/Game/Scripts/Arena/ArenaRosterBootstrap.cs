using System;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using ArknightsFrontline.Skills;
using UnityEngine;

namespace ArknightsFrontline.Arena
{
    [Serializable]
    public sealed class ArenaOperatorSlotConfiguration
    {
        [SerializeField] private string stableKey;
        [SerializeField] private TeamId team;
        [SerializeField] private OperatorType operatorType;
        [SerializeField] private GameObject template;
        [SerializeField] private Vector3 deploymentPosition;
        [SerializeField] private bool isPlayerControlled;

        public string StableKey => stableKey;
        public TeamId Team => team;
        public OperatorType OperatorType => operatorType;
        public GameObject Template => template;
        public Vector3 DeploymentPosition => deploymentPosition;
        public bool IsPlayerControlled => isPlayerControlled;

        public ArenaOperatorSlotConfiguration(
            string stableKey,
            TeamId team,
            OperatorType operatorType,
            GameObject template,
            Vector3 deploymentPosition,
            bool isPlayerControlled)
        {
            this.stableKey = stableKey;
            this.team = team;
            this.operatorType = operatorType;
            this.template = template;
            this.deploymentPosition = deploymentPosition;
            this.isPlayerControlled = isPlayerControlled;
        }
    }

    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class ArenaRosterBootstrap : MonoBehaviour
    {
        [SerializeField] private OperatorRosterController roster;
        [SerializeField] private CombatUnit blueTower;
        [SerializeField] private CombatUnit redTower;
        [SerializeField] private MatchOutcomeController match;
        [SerializeField] private PlayerDeploymentPresenter playerDeployment;
        [SerializeField] private SkillHudPresenter skillHud;
        [SerializeField] private MobaCameraController cameraController;
        [SerializeField] private ArenaOperatorSlotConfiguration[] slots = Array.Empty<ArenaOperatorSlotConfiguration>();

        public OperatorRosterController Roster => roster;

        public void Configure(
            OperatorRosterController operatorRoster,
            CombatUnit friendlyBlueTower,
            CombatUnit friendlyRedTower,
            MatchOutcomeController matchController,
            PlayerDeploymentPresenter deploymentPresenter,
            SkillHudPresenter hudPresenter,
            MobaCameraController mobaCamera,
            ArenaOperatorSlotConfiguration[] slotConfigurations)
        {
            roster = operatorRoster;
            blueTower = friendlyBlueTower;
            redTower = friendlyRedTower;
            match = matchController;
            playerDeployment = deploymentPresenter;
            skillHud = hudPresenter;
            cameraController = mobaCamera;
            slots = slotConfigurations ?? Array.Empty<ArenaOperatorSlotConfiguration>();
        }

        private void Awake()
        {
            if (roster == null) roster = GetComponent<OperatorRosterController>();
            if (roster == null || blueTower == null || redTower == null || match == null
                || playerDeployment == null || skillHud == null || cameraController == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(ArenaRosterBootstrap)} requires its roster, towers, match, player feedback, and camera references.");
            }

            if (slots == null || slots.Length != 6)
            {
                throw new InvalidOperationException("The saved arena requires six operator slot configurations.");
            }

            PlayerCharacterSelection selection = GetComponent<PlayerCharacterSelection>();
            if (selection != null)
            {
                selection.Configure(StartSelectedMatch);
                return;
            }
            StartSelectedMatch(null);
        }

        private void StartSelectedMatch(GameObject selectedTemplate)
        {
            foreach (ArenaOperatorSlotConfiguration configuration in slots)
            {
                if (configuration == null)
                {
                    throw new InvalidOperationException("The saved arena contains an empty operator slot configuration.");
                }

                roster.RegisterSlot(
                    configuration.StableKey,
                    configuration.Team,
                    configuration.IsPlayerControlled && selectedTemplate != null ? OperatorType.NiuLai : configuration.OperatorType,
                    configuration.IsPlayerControlled && selectedTemplate != null ? selectedTemplate : configuration.Template,
                    configuration.DeploymentPosition,
                    configuration.IsPlayerControlled);
            }

            playerDeployment.Configure(roster, "Player_Exusiai", skillHud, cameraController);
            roster.OperatorSpawned += OnOperatorSpawned;
            roster.StartMatch();
        }

        private void OnDestroy()
        {
            if (roster != null)
            {
                roster.OperatorSpawned -= OnOperatorSpawned;
            }
        }

        private void OnOperatorSpawned(OperatorRosterSlot slot, CombatUnit liveOperator)
        {
            if (slot == null || liveOperator == null)
            {
                return;
            }

            OperatorRetreatController retreat = liveOperator.GetComponent<OperatorRetreatController>();
            if (retreat != null)
            {
                retreat.Configure(match);
            }

            if (slot.IsPlayerControlled)
            {
                return;
            }

            CombatUnit friendlyTower = slot.Team == TeamId.Blue ? blueTower : redTower;
            OperatorIdentity identity = liveOperator.GetComponent<OperatorIdentity>();
            SimpleOperatorAiController ai = liveOperator.GetComponent<SimpleOperatorAiController>();
            if (identity == null || ai == null)
            {
                throw new MissingComponentException(
                    $"Computer operator {slot.StableKey} requires identity and AI components on its template.");
            }

            ai.Configure(
                identity,
                slot,
                friendlyTower,
                retreat,
                ArenaLayout.CreateDefault(),
                match);
        }
    }
}
