using ArknightsFrontline.Arena;
using ArknightsFrontline.Camera;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Input;
using ArknightsFrontline.Movement;
using ArknightsFrontline.Skills;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArknightsFrontline.Commands
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UnitMotor))]
    public sealed class PlayerCommandController : MonoBehaviour
    {
        private readonly AttackMoveState attackMoveState = new AttackMoveState();
        private readonly ArenaLayout layout = ArenaLayout.CreateDefault();

        private GameInputActions input;
        private UnitMotor motor;
        private GameObject currentTarget;
        private InputControl consumedCancelControl;
        private int consumedCancelFrame = -1;
        private bool hasPendingMoveClick;
        private bool pendingMoveClickWasArmed;
        private InputControl pendingMoveControl;
        private bool isAttackMoveHeld;
        private IPlayerSkillInputHandler skillInputHandler;
        private OperatorRetreatController retreat;
        private Ray cachedPointerRay;
        private bool hasCachedPointerRay;
        private RaycastHit cachedPointerHit;
        private bool hasCachedPointerHit;
        private int cachedPointerFrame = -1;
        private bool stoppedForMatch;

        public GameObject CurrentTarget => currentTarget;

        public UnitCommand? CurrentCommand { get; private set; }

        public int CommandRevision { get; private set; }

        public bool IsAttackMoveArmed => attackMoveState.IsArmed;

        public bool IsAttackMoveHeld => isAttackMoveHeld;

        public bool IsRetreatGuiding => retreat != null && retreat.IsGuiding;

        public bool IsStoppedForMatch => stoppedForMatch;

        public InputActionAsset InputActions => input.Asset;

        private void Awake()
        {
            if (input != null)
            {
                return;
            }

            motor = GetComponent<UnitMotor>();
            retreat = GetComponent<OperatorRetreatController>();
            if (retreat == null)
            {
                retreat = gameObject.AddComponent<OperatorRetreatController>();
            }
            retreat.Configure();
            input = new GameInputActions();
            InputBindingStore.Load(input.Asset);
            input.MoveClick.performed += OnMoveClick;
            input.AttackMove.performed += OnAttackMove;
            input.AttackMove.canceled += OnAttackMoveCanceled;
            input.Confirm.performed += OnConfirm;
            input.Stop.performed += OnStop;
            input.Cancel.performed += OnCancel;
            input.Skill2.performed += OnSkill2;
            input.Skill1.performed += OnSkill1;
            input.Skill3.performed += OnSkill3;
            input.Retreat.performed += OnRetreat;
            input.CenterCamera.performed += OnCenterCamera;
        }

        private void OnEnable()
        {
            if (!stoppedForMatch)
            {
                input?.Gameplay.Enable();
            }
        }

        private void OnDisable()
        {
            isAttackMoveHeld = false;
            attackMoveState.Cancel();
            hasPendingMoveClick = false;
            pendingMoveClickWasArmed = false;
            pendingMoveControl = null;
            ClearPointerHitCache();
            input?.Gameplay.Disable();
        }

        private void OnDestroy()
        {
            if (input == null)
            {
                return;
            }

            input.MoveClick.performed -= OnMoveClick;
            input.AttackMove.performed -= OnAttackMove;
            input.AttackMove.canceled -= OnAttackMoveCanceled;
            input.Confirm.performed -= OnConfirm;
            input.Stop.performed -= OnStop;
            input.Cancel.performed -= OnCancel;
            input.Skill2.performed -= OnSkill2;
            input.Skill1.performed -= OnSkill1;
            input.Skill3.performed -= OnSkill3;
            input.Retreat.performed -= OnRetreat;
            input.CenterCamera.performed -= OnCenterCamera;
            input.Dispose();
        }

        public void SetSkillInputHandler(IPlayerSkillInputHandler handler)
        {
            if (stoppedForMatch)
            {
                return;
            }

            skillInputHandler = handler;
        }

        public void Issue(UnitCommand command)
        {
            if (stoppedForMatch)
            {
                return;
            }

            if (IsRetreatGuiding)
            {
                return;
            }

            CombatUnit combatUnit = GetComponent<CombatUnit>();
            if (combatUnit != null && combatUnit.IsDead)
            {
                return;
            }

            CurrentCommand = command;
            CommandRevision++;
            switch (command.Kind)
            {
                case UnitCommandKind.Move:
                    currentTarget = null;
                    motor.SetDestination(command.Destination);
                    break;
                case UnitCommandKind.Attack:
                    motor.Stop();
                    currentTarget = command.TargetObject;
                    break;
                case UnitCommandKind.AttackNearestInRange:
                    currentTarget = null;
                    motor.Stop();
                    break;
                case UnitCommandKind.Stop:
                    currentTarget = null;
                    motor.Stop();
                    break;
            }
        }

        public void CancelCurrentCommand()
        {
            if (stoppedForMatch)
            {
                return;
            }

            CurrentCommand = null;
            currentTarget = null;
            motor.Stop();
            CommandRevision++;
        }

        public void StopForMatch()
        {
            if (stoppedForMatch)
            {
                return;
            }

            CurrentCommand = null;
            currentTarget = null;
            motor?.Stop();
            CommandRevision++;
            CancelAttackMove();
            hasPendingMoveClick = false;
            pendingMoveClickWasArmed = false;
            pendingMoveControl = null;
            ClearPointerHitCache();
            skillInputHandler = null;
            stoppedForMatch = true;
            input?.Gameplay.Disable();
            enabled = false;
        }

        public void ArmAttackMove()
        {
            if (stoppedForMatch || IsRetreatGuiding)
            {
                return;
            }

            attackMoveState.Arm();
        }

        public void CancelForRetreat()
        {
            CancelAttackMove();
            hasPendingMoveClick = false;
            pendingMoveClickWasArmed = false;
            pendingMoveControl = null;
            ClearPointerHitCache();
            CancelCurrentCommand();
        }

        public void HandleMoveClick()
        {
            if (stoppedForMatch || IsRetreatGuiding)
            {
                return;
            }

            if (attackMoveState.IsArmed)
            {
                CancelAttackMove();
                return;
            }

            if (!TryGetPointerHit(out RaycastHit hit))
            {
                return;
            }

            HandleMoveClick(hit);
        }

        private void OnMoveClick(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsRetreatGuiding)
            {
                return;
            }

            QueueMoveClick(context.control);
        }

        private void OnAttackMove(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsRetreatGuiding)
            {
                return;
            }

            if (skillInputHandler != null && skillInputHandler.BlocksAttackMove)
            {
                return;
            }

            isAttackMoveHeld = true;
            ArmAttackMove();
        }

        private void OnAttackMoveCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            CancelAttackMove();
        }

        private void OnConfirm(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsRetreatGuiding)
            {
                return;
            }

            if (skillInputHandler == null && (!IsAttackMoveArmed || !IsAttackMoveHeld))
            {
                return;
            }

            bool hasHit = TryGetPointerHit(out RaycastHit hit);
            Vector3 worldPoint = transform.position;
            if (hasHit && TryGetPointerGroundPoint(out Vector3 groundPoint))
            {
                worldPoint = groundPoint;
            }
            GameObject hitObject = hasHit ? hit.collider.gameObject : null;
            if (skillInputHandler != null && skillInputHandler.TryHandleConfirm(worldPoint, hitObject))
            {
                return;
            }

            if (skillInputHandler != null && skillInputHandler.BlocksNormalCommands)
            {
                return;
            }

            if (!IsAttackMoveArmed || !IsAttackMoveHeld)
            {
                return;
            }

            attackMoveState.Confirm();
            if (hasHit
                && hitObject.layer == LayerMask.NameToLayer("Targetable")
                && hitObject.TryGetComponent(out CombatUnit target)
                && TargetRules.IsLegal(GetComponent<CombatUnit>(), target))
            {
                Issue(UnitCommand.Attack(hitObject));
                return;
            }

            Issue(UnitCommand.AttackNearestInRange());
        }

        private void OnStop(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsRetreatGuiding)
            {
                return;
            }

            skillInputHandler?.HandleStop();
            CancelAttackMove();
            Issue(UnitCommand.Stop());
        }

        private void OnCancel(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsRetreatGuiding)
            {
                return;
            }

            if (skillInputHandler != null && skillInputHandler.TryHandleCancel())
            {
                CancelAttackMove();
                ConsumeCancelInput(context.control);
                return;
            }

            if (!attackMoveState.IsArmed && !isAttackMoveHeld)
            {
                return;
            }

            CancelAttackMove();
            ConsumeCancelInput(context.control);
        }

        private void OnSkill1(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!IsRetreatGuiding && skillInputHandler is NiuLaiSkillController cow)
                cow.ActivateEmpower();
        }

        private void OnSkill2(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsRetreatGuiding)
            {
                return;
            }

            skillInputHandler?.HandleSkill2();
        }

        private void OnSkill3(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (IsRetreatGuiding)
            {
                return;
            }

            skillInputHandler?.HandleSkill3();
        }

        private void OnRetreat(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!IsRetreatGuiding)
            {
                retreat?.TryBegin();
            }
        }

        private void Update()
        {
            RefreshPointerHitCache();
            if (IsRetreatGuiding)
            {
                hasPendingMoveClick = false;
                pendingMoveClickWasArmed = false;
                pendingMoveControl = null;
                return;
            }

            if (!hasPendingMoveClick)
            {
                return;
            }

            bool wasArmed = pendingMoveClickWasArmed;
            hasPendingMoveClick = false;
            pendingMoveControl = null;

            if (skillInputHandler != null)
            {
                bool hasHit = TryGetPointerHit(out RaycastHit hit);
                Vector3 worldPoint = hasHit ? hit.point : default;
                GameObject hitObject = hasHit ? hit.collider.gameObject : null;
                if (skillInputHandler.TryHandleMoveClick(worldPoint))
                {
                    return;
                }

                if (skillInputHandler.BlocksNormalCommands)
                {
                    return;
                }

                if (wasArmed)
                {
                    CancelAttackMove();
                    return;
                }

                if (hasHit)
                {
                    HandleMoveClick(hit);
                }

                return;
            }

            if (wasArmed)
            {
                CancelAttackMove();
                return;
            }

            HandleMoveClick();
        }

        private void OnCenterCamera(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            MobaCameraController cameraController = mainCamera.GetComponent<MobaCameraController>();
            if (cameraController != null)
            {
                cameraController.CenterOn(transform);
            }
        }

        public bool TryGetPointerHit(out RaycastHit hit)
        {
            RefreshPointerHitCache();
            hit = cachedPointerHit;
            return hasCachedPointerHit;
        }

        /// <summary>
        /// Gets the most recently sampled pointer hit without issuing a new physics query.
        /// </summary>
        public bool TryGetCachedPointerHit(out RaycastHit hit)
        {
            hit = cachedPointerHit;
            return hasCachedPointerHit;
        }

        private void RefreshPointerHitCache()
        {
            if (cachedPointerFrame == Time.frameCount)
            {
                return;
            }

            cachedPointerFrame = Time.frameCount;
            cachedPointerRay = default;
            hasCachedPointerRay = false;
            cachedPointerHit = default;
            hasCachedPointerHit = false;
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null || input == null)
            {
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(input.PointerPosition.ReadValue<Vector2>());
            cachedPointerRay = ray;
            hasCachedPointerRay = true;
            hasCachedPointerHit = Physics.Raycast(
                ray, out cachedPointerHit, Mathf.Infinity, Physics.DefaultRaycastLayers);
        }

        public bool TryGetPointerGroundPoint(out Vector3 groundPoint)
        {
            groundPoint = transform.position;
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0 || !hasCachedPointerRay)
            {
                return false;
            }

            if (!Physics.Raycast(
                    cachedPointerRay,
                    out RaycastHit groundHit,
                    Mathf.Infinity,
                    1 << groundLayer))
            {
                return false;
            }

            groundPoint = groundHit.point;
            return true;
        }

        private void ClearPointerHitCache()
        {
            cachedPointerRay = default;
            hasCachedPointerRay = false;
            cachedPointerHit = default;
            hasCachedPointerHit = false;
            cachedPointerFrame = -1;
        }

        private void CancelAttackMove()
        {
            isAttackMoveHeld = false;
            attackMoveState.Cancel();
        }

        private void ConsumeCancelInput(InputControl control)
        {
            consumedCancelControl = control;
            consumedCancelFrame = Time.frameCount;
            if (hasPendingMoveClick && pendingMoveControl == control)
            {
                hasPendingMoveClick = false;
            }
        }

        private void QueueMoveClick(InputControl control)
        {
            if (consumedCancelFrame == Time.frameCount && consumedCancelControl == control)
            {
                return;
            }

            hasPendingMoveClick = true;
            pendingMoveClickWasArmed = attackMoveState.IsArmed;
            pendingMoveControl = control;
        }

        private void HandleMoveClick(RaycastHit hit)
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Targetable"))
            {
                Issue(UnitCommand.Attack(hit.collider.gameObject));
                return;
            }

            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                Issue(UnitCommand.Move(layout.Clamp(hit.point)));
            }
        }
    }
}
