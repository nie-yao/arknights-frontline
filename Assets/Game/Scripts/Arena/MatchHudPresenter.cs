using System;
using System.Globalization;
using ArknightsFrontline.Combat;
using ArknightsFrontline.Common;
using UnityEngine;
using UnityEngine.UI;

namespace ArknightsFrontline.Arena
{
    [DisallowMultipleComponent]
    public sealed class MatchHudPresenter : MonoBehaviour
    {
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private const float ResultCardWidth = 1120f;
        private const float ResultCardHeight = 640f;
        private const float ResultCardCanvasInset = 32f;
        private const float ResultRowHeight = 42f;

        private MatchOutcomeController match;
        private MatchStatisticsController statistics;
        private TeamExitVoteController votes;
        private CombatUnit blueTower;
        private CombatUnit redTower;
        private string playerStableKey;
        private Action exitAction;
        private bool isConfigured;
        private bool exitCallbackInvoked;
        private bool uiCreated;
        private bool ownsDisplayFont;
        private float lastBlueTowerHealth;
        private float lastBlueTowerMaxHealth;
        private float lastRedTowerHealth;
        private float lastRedTowerMaxHealth;

        private GameObject liveBar;
        private GameObject resultOverlay;
        private Image resultDimmer;
        private Text resultTitleText;
        private Text resultSummaryText;
        private Text exitVoteLabel;
        private Button exitButton;
        private RectTransform resultCardRect;
        private Font displayFont;
        private MatchResultSnapshot renderedSnapshot;

        public Canvas Canvas { get; private set; }

        public string ClockText { get; private set; } = string.Empty;

        public string BlueTowerText { get; private set; } = string.Empty;

        public string RedTowerText { get; private set; } = string.Empty;

        public string ResultTitle { get; private set; } = string.Empty;

        public string ResultElapsedText { get; private set; } = string.Empty;

        public string ExitVoteText { get; private set; } = string.Empty;

        public int ResultRowCount { get; private set; }

        public bool IsResultVisible { get; private set; }

        public Transform ResultRowsRoot { get; private set; }

        public Button ExitButton => exitButton;

        public Text ClockTextComponent { get; private set; }

        public Text BlueTowerTextComponent { get; private set; }

        public Text RedTowerTextComponent { get; private set; }

        public Text ResultTitleComponent => resultTitleText;

        public Text ResultSummaryComponent => resultSummaryText;

        public Text ExitVoteTextComponent => exitVoteLabel;

        private void Update()
        {
            if (isConfigured)
            {
                Refresh();
            }
        }

        private void OnDestroy()
        {
            Unbind();
            ReleaseOwnedFont();
        }

        public void Configure(
            MatchOutcomeController match,
            MatchStatisticsController statistics,
            TeamExitVoteController votes,
            CombatUnit blueTower,
            CombatUnit redTower,
            string playerStableKey,
            Action exitAction = null)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (statistics == null)
            {
                throw new ArgumentNullException(nameof(statistics));
            }

            if (votes == null)
            {
                throw new ArgumentNullException(nameof(votes));
            }

            if (string.IsNullOrWhiteSpace(playerStableKey))
            {
                throw new ArgumentException("The player requires a stable roster key.", nameof(playerStableKey));
            }

            bool sameMatchSession = ReferenceEquals(this.match, match)
                && ReferenceEquals(this.votes, votes);
            Unbind();

            this.match = match;
            this.statistics = statistics;
            this.votes = votes;
            this.blueTower = blueTower;
            this.redTower = redTower;
            this.playerStableKey = playerStableKey;
            this.exitAction = exitAction;
            isConfigured = true;
            if (!sameMatchSession)
            {
                exitCallbackInvoked = false;
            }

            CacheTowerHealth();
            EnsureUiCreated();

            this.match.MatchResolved += OnMatchResolved;
            this.statistics.ResultReady += OnResultReady;
            this.votes.ExitApproved += OnExitApproved;
            exitButton.onClick.RemoveListener(OnExitButtonClicked);
            exitButton.onClick.AddListener(OnExitButtonClicked);

            Refresh();
        }

        /// <summary>
        /// Refreshes displayed match state. Public for deterministic presentation tests.
        /// </summary>
        public void Refresh()
        {
            if (!isConfigured)
            {
                return;
            }

            EnsureUiCreated();

            // A death presenter may destroy a tower before the final result notification.
            // Once available, the frozen snapshot always takes precedence over live references.
            MatchResultSnapshot snapshot = statistics == null ? null : statistics.Snapshot;
            if (snapshot != null)
            {
                RefreshResult(snapshot);
                return;
            }

            IsResultVisible = false;
            resultOverlay.SetActive(false);
            liveBar.SetActive(true);
            UpdateLiveTowerCache();

            ClockText = FormatElapsed(match.ElapsedSeconds);
            BlueTowerText = FormatTower("蓝塔", lastBlueTowerHealth, lastBlueTowerMaxHealth);
            RedTowerText = FormatTower("红塔", lastRedTowerHealth, lastRedTowerMaxHealth);
            ClockTextComponent.text = ClockText;
            BlueTowerTextComponent.text = BlueTowerText;
            RedTowerTextComponent.text = RedTowerText;
            RefreshVoteLabel();
        }

        private void RefreshResult(MatchResultSnapshot snapshot)
        {
            IsResultVisible = true;
            liveBar.SetActive(false);
            resultOverlay.SetActive(true);
            FitResultCardToCanvas();

            ResultTitle = GetResultTitle(snapshot.Outcome);
            ResultElapsedText = FormatElapsed(snapshot.ElapsedSeconds);
            resultTitleText.text = ResultTitle;
            string blueTowerSummary = FormatTower(
                "蓝塔",
                snapshot.BlueTowerCurrentHealth,
                snapshot.BlueTowerMaxHealth);
            string redTowerSummary = FormatTower(
                "红塔",
                snapshot.RedTowerCurrentHealth,
                snapshot.RedTowerMaxHealth);
            resultSummaryText.text = $"比赛时长  {ResultElapsedText}\n{blueTowerSummary}     {redTowerSummary}";

            RenderRows(snapshot);
            RefreshVoteLabel();
            exitButton.interactable = votes != null && votes.IsConfigured && !votes.IsExitApproved;
        }

        private void FitResultCardToCanvas()
        {
            if (resultCardRect == null || Canvas == null)
            {
                return;
            }

            RectTransform canvasRect = Canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Rect bounds = canvasRect.rect;
            float availableWidth = Mathf.Max(0f, bounds.width - ResultCardCanvasInset * 2f);
            float availableHeight = Mathf.Max(0f, bounds.height - ResultCardCanvasInset * 2f);
            float scale = Mathf.Min(
                1f,
                Mathf.Min(availableWidth / ResultCardWidth, availableHeight / ResultCardHeight));
            resultCardRect.localScale = new Vector3(scale, scale, 1f);
        }

        private void RenderRows(MatchResultSnapshot snapshot)
        {
            if (ReferenceEquals(renderedSnapshot, snapshot))
            {
                return;
            }

            renderedSnapshot = snapshot;
            for (int index = ResultRowsRoot.childCount - 1; index >= 0; index--)
            {
                DestroyUiObject(ResultRowsRoot.GetChild(index).gameObject);
            }

            ResultRowCount = snapshot.Rows.Count;
            for (int index = 0; index < snapshot.Rows.Count; index++)
            {
                MatchResultRow row = snapshot.Rows[index];
                GameObject rowObject = CreatePanel(
                    $"ResultRow{index + 1}",
                    ResultRowsRoot,
                    index % 2 == 0
                        ? new Color(0.13f, 0.17f, 0.22f, 0.96f)
                        : new Color(0.10f, 0.13f, 0.18f, 0.96f),
                    raycastTarget: false);
                RectTransform rowRect = (RectTransform)rowObject.transform;
                rowRect.anchorMin = Center;
                rowRect.anchorMax = Center;
                rowRect.pivot = Center;
                rowRect.sizeDelta = new Vector2(1040f, ResultRowHeight);
                rowRect.anchoredPosition = new Vector2(0f, 125f - index * 48f);

                Text rowLabel = CreateText(
                    "RowText",
                    rowObject.transform,
                    Vector2.zero,
                    Vector2.one,
                    Center,
                    new Vector2(-24f, -2f),
                    Vector2.zero,
                    20,
                    TextAnchor.MiddleLeft,
                    FontStyle.Normal);
                rowLabel.text = FormatRow(row);
            }
        }

        private void RefreshVoteLabel()
        {
            int required = votes == null ? 3 : votes.RequiredCount;
            int agreed = votes == null ? 0 : Mathf.Clamp(votes.AgreeCount, 0, required);
            ExitVoteText = $"己方同意：{agreed}/{required}";
            if (exitVoteLabel != null)
            {
                exitVoteLabel.text = ExitVoteText;
            }
        }

        private void UpdateLiveTowerCache()
        {
            if (blueTower != null)
            {
                lastBlueTowerHealth = Mathf.Clamp(blueTower.CurrentHealth, 0f, blueTower.MaxHealth);
                lastBlueTowerMaxHealth = blueTower.MaxHealth;
            }

            if (redTower != null)
            {
                lastRedTowerHealth = Mathf.Clamp(redTower.CurrentHealth, 0f, redTower.MaxHealth);
                lastRedTowerMaxHealth = redTower.MaxHealth;
            }
        }

        private void CacheTowerHealth()
        {
            lastBlueTowerHealth = blueTower == null ? 0f : Mathf.Max(0f, blueTower.CurrentHealth);
            lastBlueTowerMaxHealth = blueTower == null ? 0f : Mathf.Max(0f, blueTower.MaxHealth);
            lastRedTowerHealth = redTower == null ? 0f : Mathf.Max(0f, redTower.CurrentHealth);
            lastRedTowerMaxHealth = redTower == null ? 0f : Mathf.Max(0f, redTower.MaxHealth);
        }

        private void EnsureUiCreated()
        {
            if (uiCreated)
            {
                return;
            }

            Canvas = GetComponentInParent<Canvas>();
            if (Canvas == null)
            {
                throw new MissingComponentException(
                    $"{nameof(MatchHudPresenter)} must be placed beneath the arena's existing Canvas.");
            }

            RectTransform hudRect = transform as RectTransform;
            if (hudRect == null)
            {
                throw new MissingComponentException(
                    $"{nameof(MatchHudPresenter)} requires a RectTransform beneath the existing Canvas.");
            }

            // This component owns only its own root layout. It deliberately leaves the shared
            // Canvas and sibling HUD presenters untouched.
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.pivot = Center;
            hudRect.sizeDelta = Vector2.zero;
            hudRect.anchoredPosition = Vector2.zero;

            liveBar = CreatePanel(
                "MatchLiveBar",
                transform,
                new Color(0.035f, 0.055f, 0.075f, 0.88f),
                raycastTarget: false);
            RectTransform liveRect = (RectTransform)liveBar.transform;
            liveRect.anchorMin = new Vector2(0f, 1f);
            liveRect.anchorMax = new Vector2(1f, 1f);
            liveRect.pivot = new Vector2(0.5f, 1f);
            liveRect.sizeDelta = new Vector2(0f, 66f);
            liveRect.anchoredPosition = new Vector2(0f, -6f);

            ClockTextComponent = CreateText(
                "MatchClock",
                liveBar.transform,
                new Vector2(0.36f, 0f),
                new Vector2(0.64f, 1f),
                Center,
                Vector2.zero,
                Vector2.zero,
                24,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            BlueTowerTextComponent = CreateText(
                "BlueTowerHealth",
                liveBar.transform,
                new Vector2(0.025f, 0f),
                new Vector2(0.34f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                22,
                TextAnchor.MiddleLeft,
                FontStyle.Bold,
                new Color(0.55f, 0.78f, 1f));
            RedTowerTextComponent = CreateText(
                "RedTowerHealth",
                liveBar.transform,
                new Vector2(0.66f, 0f),
                new Vector2(0.975f, 1f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                22,
                TextAnchor.MiddleRight,
                FontStyle.Bold,
                new Color(1f, 0.64f, 0.64f));

            resultOverlay = new GameObject("MatchResultOverlay", typeof(RectTransform), typeof(Image));
            resultOverlay.transform.SetParent(transform, false);
            RectTransform overlayRect = (RectTransform)resultOverlay.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.pivot = Center;
            overlayRect.sizeDelta = Vector2.zero;
            overlayRect.anchoredPosition = Vector2.zero;
            resultDimmer = resultOverlay.GetComponent<Image>();
            resultDimmer.color = new Color(0.015f, 0.025f, 0.04f, 0.66f);
            resultDimmer.raycastTarget = true;

            GameObject card = CreatePanel(
                "ResultCard",
                resultOverlay.transform,
                new Color(0.055f, 0.075f, 0.10f, 0.99f),
                raycastTarget: true);
            resultCardRect = (RectTransform)card.transform;
            resultCardRect.anchorMin = Center;
            resultCardRect.anchorMax = Center;
            resultCardRect.pivot = Center;
            resultCardRect.sizeDelta = new Vector2(ResultCardWidth, ResultCardHeight);
            resultCardRect.anchoredPosition = Vector2.zero;

            resultTitleText = CreateText(
                "ResultTitle",
                card.transform,
                new Vector2(0.05f, 0.84f),
                new Vector2(0.95f, 0.98f),
                Center,
                Vector2.zero,
                Vector2.zero,
                36,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                Color.white);
            resultSummaryText = CreateText(
                "ResultSummary",
                card.transform,
                new Vector2(0.06f, 0.68f),
                new Vector2(0.94f, 0.84f),
                Center,
                Vector2.zero,
                Vector2.zero,
                21,
                TextAnchor.MiddleCenter,
                FontStyle.Normal,
                new Color(0.90f, 0.93f, 0.97f));

            GameObject rowsObject = new GameObject("ResultRows", typeof(RectTransform));
            rowsObject.transform.SetParent(card.transform, false);
            RectTransform rowsRect = (RectTransform)rowsObject.transform;
            rowsRect.anchorMin = Center;
            rowsRect.anchorMax = Center;
            rowsRect.pivot = Center;
            rowsRect.sizeDelta = new Vector2(1040f, 300f);
            rowsRect.anchoredPosition = new Vector2(0f, -4f);
            ResultRowsRoot = rowsObject.transform;

            exitVoteLabel = CreateText(
                "ExitVoteCount",
                card.transform,
                new Vector2(0.18f, 0.11f),
                new Vector2(0.82f, 0.20f),
                Center,
                Vector2.zero,
                Vector2.zero,
                20,
                TextAnchor.MiddleCenter,
                FontStyle.Normal,
                new Color(0.88f, 0.91f, 0.96f));

            GameObject buttonObject = new GameObject("ExitGameButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(card.transform, false);
            RectTransform buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = Center;
            buttonRect.anchorMax = Center;
            buttonRect.pivot = Center;
            buttonRect.sizeDelta = new Vector2(280f, 54f);
            buttonRect.anchoredPosition = new Vector2(0f, -274f);
            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.14f, 0.36f, 0.58f, 1f);
            buttonImage.raycastTarget = true;
            exitButton = buttonObject.GetComponent<Button>();
            exitButton.targetGraphic = buttonImage;
            ColorBlock colors = exitButton.colors;
            colors.highlightedColor = new Color(0.22f, 0.49f, 0.72f, 1f);
            colors.pressedColor = new Color(0.09f, 0.25f, 0.40f, 1f);
            exitButton.colors = colors;
            Text exitButtonLabel = CreateText(
                "ExitGameLabel",
                buttonObject.transform,
                Vector2.zero,
                Vector2.one,
                Center,
                new Vector2(-16f, -8f),
                Vector2.zero,
                22,
                TextAnchor.MiddleCenter,
                FontStyle.Bold,
                Color.white);
            exitButtonLabel.text = "退出游戏";

            resultOverlay.SetActive(false);
            uiCreated = true;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color, bool raycastTarget)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return panel;
        }

        private Text CreateText(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position,
            int fontSize,
            TextAnchor alignment,
            FontStyle fontStyle,
            Color? color = null)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = anchorMin;
            labelRect.anchorMax = anchorMax;
            labelRect.pivot = pivot;
            labelRect.sizeDelta = size;
            labelRect.anchoredPosition = position;

            Text label = labelObject.GetComponent<Text>();
            label.font = GetDisplayFont();
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color ?? Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Font GetDisplayFont()
        {
            if (displayFont != null)
            {
                return displayFont;
            }

            displayFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun", "Arial" },
                24);
            ownsDisplayFont = displayFont != null;
            if (displayFont == null)
            {
                displayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return displayFont;
        }

        private void ReleaseOwnedFont()
        {
            if (!ownsDisplayFont || displayFont == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(displayFont);
            }
            else
            {
                DestroyImmediate(displayFont);
            }

            displayFont = null;
            ownsDisplayFont = false;
        }

        private void OnExitButtonClicked()
        {
            if (!IsResultVisible || votes == null || exitCallbackInvoked)
            {
                return;
            }

            votes.RequestExit(playerStableKey);
            Refresh();
        }

        private void OnExitApproved()
        {
            if (exitCallbackInvoked)
            {
                return;
            }

            exitCallbackInvoked = true;
            if (exitAction != null)
            {
                exitAction.Invoke();
            }
            else
            {
                QuitApplication();
            }

            Refresh();
        }

        private void OnMatchResolved()
        {
            Refresh();
        }

        private void OnResultReady(MatchResultSnapshot _)
        {
            Refresh();
        }

        private void Unbind()
        {
            if (match != null)
            {
                match.MatchResolved -= OnMatchResolved;
            }

            if (statistics != null)
            {
                statistics.ResultReady -= OnResultReady;
            }

            if (votes != null)
            {
                votes.ExitApproved -= OnExitApproved;
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitButtonClicked);
            }
        }

        private static string FormatElapsed(float elapsedSeconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture)
                + ":"
                + seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        private static string FormatTower(string label, float currentHealth, float maxHealth)
        {
            return $"{label}  {FormatNumber(currentHealth)}/{FormatNumber(maxHealth)}";
        }

        private static string FormatRow(MatchResultRow row)
        {
            string team = row.Team == TeamId.Blue ? "蓝队" : "红队";
            return $"{team}   {GetOperatorName(row.OperatorType)}    击杀 {row.Kills}    死亡 {row.Deaths}    对塔伤害 {FormatNumber(row.TowerDamage)}";
        }

        private static string GetOperatorName(OperatorType operatorType)
        {
            switch (operatorType)
            {
                case OperatorType.Exusiai:
                    return "能天使";
                case OperatorType.Eyjafjalla:
                    return "艾雅法拉";
                case OperatorType.SilverAsh:
                    return "银灰";
                case OperatorType.NiuLai:
                    return "牛来";
                default:
                    return operatorType.ToString();
            }
        }

        private static string GetResultTitle(MatchOutcome outcome)
        {
            switch (outcome)
            {
                case MatchOutcome.BlueVictory:
                    return "胜利";
                case MatchOutcome.RedVictory:
                    return "失败";
                case MatchOutcome.Draw:
                    return "平局";
                default:
                    return "比赛结束";
            }
        }

        private static string FormatNumber(float value)
        {
            return Mathf.Max(0f, value).ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static void DestroyUiObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
