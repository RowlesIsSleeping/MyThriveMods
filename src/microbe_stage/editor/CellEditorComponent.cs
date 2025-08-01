﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutoEvo;
using DefaultEcs;
using Godot;
using Newtonsoft.Json;
using Systems;

/// <summary>
///   The cell editor component combining the organelle and other editing logic with the GUI for it
/// </summary>
[SceneLoadedClass("res://src/microbe_stage/editor/CellEditorComponent.tscn", UsesEarlyResolve = false)]
public partial class CellEditorComponent :
    HexEditorComponentBase<ICellEditorData, CombinedEditorAction, EditorAction, OrganelleTemplate, CellType>,
    ICellEditorComponent
{
    [Export]
    public bool IsMulticellularEditor;

    [Export]
    public bool IsMacroscopicEditor;

    [Export]
    public int MaxToleranceWarnings = 3;

    /// <summary>
    ///   Temporary hex memory for use by the main thread in this component
    /// </summary>
    private readonly List<Hex> hexTemporaryMemory = new();

    private readonly List<Hex> hexTemporaryMemory2 = new();

    private readonly List<Hex> islandResults = new();
    private readonly HashSet<Hex> islandsWorkMemory1 = new();
    private readonly List<Hex> islandsWorkMemory2 = new();
    private readonly Queue<Hex> islandsWorkMemory3 = new();

    private readonly Dictionary<Compound, float> processSpeedWorkMemory = new();

    private readonly List<ShaderMaterial> temporaryDisplayerFetchList = new();

    private readonly List<EditorUserOverride> ignoredEditorWarnings = new();

#pragma warning disable CA2213

    // Selection menu tab selector buttons
    [Export]
    private Button structureTabButton = null!;

    [Export]
    private Button appearanceTabButton = null!;

    [Export]
    private Button behaviourTabButton = null!;

    [Export]
    private Button growthOrderTabButton = null!;

    [Export]
    private Button toleranceTabButton = null!;

    [Export]
    private PanelContainer structureTab = null!;

    [Export]
    private PanelContainer appearanceTab = null!;

    [JsonProperty]
    [AssignOnlyChildItemsOnDeserialize]
    [Export]
    private BehaviourEditorSubComponent behaviourEditor = null!;

    [Export]
    private PanelContainer growthOrderTab = null!;

    [Export]
    [JsonProperty]
    [AssignOnlyChildItemsOnDeserialize]
    private GrowthOrderPicker growthOrderGUI = null!;

    [Export]
    [JsonProperty]
    [AssignOnlyChildItemsOnDeserialize]
    private TolerancesEditorSubComponent tolerancesEditor = null!;

    [Export]
    private PanelContainer toleranceTab = null!;

    [Export]
    private Container toleranceWarningContainer = null!;

    [Export]
    private VBoxContainer partsSelectionContainer = null!;

    [Export]
    private CollapsibleList membraneTypeSelection = null!;

    [Export]
    private CellStatsIndicator totalEnergyLabel = null!;

    [Export]
    private Label autoEvoPredictionFailedLabel = null!;

    [Export]
    private Label bestPatchLabel = null!;

    [Export]
    private Label worstPatchLabel = null!;

    [Export]
    private Control organelleSuggestionLoadingIndicator = null!;

    [Export]
    private Label organelleSuggestionLabel = null!;

    [Export]
    private Control autoEvoPredictionPanel = null!;

    [Export]
    private Slider rigiditySlider = null!;

    [Export]
    private TweakedColourPicker membraneColorPicker = null!;

    [Export]
    private CustomConfirmationDialog negativeAtpPopup = null!;

    [Export]
    private CustomConfirmationDialog pendingEndosymbiosisPopup = null!;

    [Export]
    private Button endosymbiosisButton = null!;

    [Export]
    private EndosymbiosisPopup endosymbiosisPopup = null!;

    [Export]
    private OrganellePopupMenu organelleMenu = null!;

    [Export]
    private OrganelleUpgradeGUI organelleUpgradeGUI = null!;

    [Export]
    private CheckBox showGrowthOrderCoordinates = null!;

    [Export]
    private Control growthOrderNumberContainer = null!;

    [Export]
    private PopupMicheViewer micheViewer = null!;

    [Export]
    private OrganismStatisticsPanel organismStatisticsPanel = null!;

    [Export]
    private CustomWindow autoEvoPredictionExplanationPopup = null!;

    [Export]
    private CustomRichTextLabel autoEvoPredictionExplanationLabel = null!;

    [Export]
    private Control rightPanel = null!;

    [Export]
    private ScrollContainer rightPanelScrollContainer = null!;

    [Export]
    private Control bottomRightPanel = null!;

    private PackedScene organelleSelectionButtonScene = null!;

    private PackedScene undiscoveredOrganellesScene = null!;

    private PackedScene undiscoveredOrganellesTooltipScene = null!;

    private Node3D? cellPreviewVisualsRoot;

    [Export]
    private AnimationPlayer tutorialAnimationPlayer = null!;

    [Export]
    private LabelSettings toleranceWarningsFont = null!;
#pragma warning restore CA2213

    private OrganelleDefinition nucleus = null!;
    private OrganelleDefinition bindingAgent = null!;

    private OrganelleDefinition cytoplasm = null!;

    private EnergyBalanceInfoFull? energyBalanceInfo;

    private string? bestPatchName;

    // This and worstPatchPopulation used to be displayed but are now kept for potential future use
    private long bestPatchPopulation;

    private float bestPatchEnergyGathered;

    private string? worstPatchName;

    private long worstPatchPopulation;

    private float worstPatchEnergyGathered;

    private Dictionary<OrganelleDefinition, MicrobePartSelection> placeablePartSelectionElements = new();

    private Dictionary<OrganelleDefinition, MicrobePartSelection> allPartSelectionElements = new();

    private Dictionary<MembraneType, MicrobePartSelection> membraneSelectionElements = new();

    [JsonProperty]
    private SelectionMenuTab selectedSelectionMenuTab = SelectionMenuTab.Structure;

    private bool? autoEvoPredictionRunSuccessful;
    private PendingAutoEvoPrediction? waitingForPrediction;
    private LocalizedStringBuilder? predictionDetailsText;
    private BehaviourDictionary? overwriteBehaviourForCalculations;

    private Miche? predictionMiches;

    private OrganelleSuggestionCalculation? inProgressSuggestion;
    private bool suggestionDirty;
    private double suggestionStartTimer;

    private bool autoEvoPredictionDirty;
    private double autoEvoPredictionStartTimer;

    private bool refreshTolerancesWarnings = true;

    /// <summary>
    ///   The new to set on the species (or cell type) after exiting (if null, no change)
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This is now nullable to make loading older saves with the new editor data structures easier
    ///   </para>
    /// </remarks>
    [JsonProperty]
    private string newName = "unset";

    /// <summary>
    ///   We're taking advantage of the available membrane and organelle system already present in the microbe stage
    ///   for the membrane preview.
    /// </summary>
    private MicrobeVisualOnlySimulation? previewSimulation;

    private MicrobeSpecies? previewMicrobeSpecies;
    private Entity previewMicrobe;

    [JsonProperty]
    private Color colour;

    [JsonProperty]
    private float rigidity;

    /// <summary>
    ///   To not have to recreate this object for each place / remove this is a cached clone of editedSpecies to which
    ///   current editor changes are applied for simulating what effect they would have on the population.
    /// </summary>
    private MicrobeSpecies? cachedAutoEvoPredictionSpecies;

    /// <summary>
    ///   This is the container that has the edited organelles in
    ///   it. This is populated when entering and used to update the
    ///   player's species template on exit.
    /// </summary>
    [JsonProperty]
    private OrganelleLayout<OrganelleTemplate> editedMicrobeOrganelles = null!;

    /// <summary>
    ///   When this is true, on the next process this will handle added and removed organelles and update stats etc.
    ///   This is done to make adding a bunch of organelles at once more efficient.
    /// </summary>
    private bool organelleDataDirty = true;

    /// <summary>
    ///   Similar to organelleDataDirty but with the exception that this is only set false when the editor
    ///   membrane mesh has been redone. Used so the membrane doesn't have to be rebuilt every time when
    ///   switching back and forth between structure and membrane tab (without editing organelle placements).
    /// </summary>
    private bool microbeVisualizationOrganellePositionsAreDirty = true;

    private bool microbePreviewMode;

    private bool showGrowthOrderNumbers;

    private bool multicellularTolerancesPrinted;

    private TutorialState? tutorialState;

    public enum SelectionMenuTab
    {
        Structure,
        Membrane,
        Behaviour,
        GrowthOrder,
        Tolerance,
    }

    /// <summary>
    ///   The selected membrane rigidity
    /// </summary>
    [JsonIgnore]
    public float Rigidity
    {
        get => rigidity;
        set
        {
            rigidity = value;

            if (previewMicrobeSpecies == null)
                return;

            previewMicrobeSpecies.MembraneRigidity = value;

            if (previewMicrobe.IsAlive)
                previewSimulation!.ApplyMicrobeRigidity(previewMicrobe, previewMicrobeSpecies.MembraneRigidity);
        }
    }

    /// <summary>
    ///   Selected membrane type for the species
    /// </summary>
    [JsonProperty]
    public MembraneType Membrane { get; private set; } = null!;

    /// <summary>
    ///   Current selected colour for the species.
    /// </summary>
    [JsonIgnore]
    public Color Colour
    {
        get => colour;
        set
        {
            colour = value;

            if (previewMicrobeSpecies == null)
                return;

            previewMicrobeSpecies.Colour = value;

            if (previewMicrobe.IsAlive)
                previewSimulation!.ApplyMicrobeColour(previewMicrobe, previewMicrobeSpecies.Colour);
        }
    }

    /// <summary>
    ///   The name of organelle type that is selected to be placed
    /// </summary>
    [JsonIgnore]
    public string? ActiveActionName
    {
        get => activeActionName;
        set
        {
            if (value != activeActionName)
            {
                TutorialState?.SendEvent(TutorialEventType.MicrobeEditorOrganelleToPlaceChanged,
                    new StringEventArgs(value), this);
            }

            activeActionName = value;
        }
    }

    [JsonIgnore]
    public override bool CanCancelAction => base.CanCancelAction || PendingEndosymbiontPlace != null;

    [JsonProperty]
    public EndosymbiontPlaceActionData? PendingEndosymbiontPlace { get; protected set; }

    /// <summary>
    ///   If this is enabled the editor will show how the edited cell would look like in the environment with
    ///   parameters set in the editor. Editing hexes is disabled during this (except undo / redo).
    /// </summary>
    public bool MicrobePreviewMode
    {
        get => microbePreviewMode;
        set
        {
            microbePreviewMode = value;

            if (cellPreviewVisualsRoot != null)
                cellPreviewVisualsRoot.Visible = value;

            // Need to reapply the species as changes to it are ignored when the appearance tab is not shown
            UpdateCellVisualization();

            foreach (var hex in placedHexes)
                hex.Visible = !MicrobePreviewMode;

            foreach (var model in placedModels)
                model.Visible = !MicrobePreviewMode;
        }
    }

    /// <summary>
    ///   When enabled numbers are shown above the organelles to indicate their growth order
    /// </summary>
    public bool ShowGrowthOrder
    {
        get => showGrowthOrderNumbers;
        set
        {
            showGrowthOrderNumbers = value;

            UpdateGrowthOrderButtons();
        }
    }

    [JsonIgnore]
    public bool HasNucleus => PlacedUniqueOrganelles.Any(d => d == nucleus);

    [JsonIgnore]
    public override bool HasIslands =>
        editedMicrobeOrganelles.GetIslandHexes(islandResults, islandsWorkMemory1, islandsWorkMemory2,
            islandsWorkMemory3) > 0;

    /// <summary>
    ///   Number of organelles in the microbe
    /// </summary>
    [JsonIgnore]
    public int MicrobeSize => editedMicrobeOrganelles.Organelles.Count;

    /// <summary>
    ///   Number of hexes in the microbe
    /// </summary>
    [JsonIgnore]
    public int MicrobeHexSize
    {
        get
        {
            int result = 0;

            foreach (var organelle in editedMicrobeOrganelles.Organelles)
            {
                result += organelle.Definition.HexCount;
            }

            return result;
        }
    }

    [JsonIgnore]
    public TutorialState? TutorialState
    {
        get => tutorialState;
        set
        {
            tutorialState = value;

            if (tutorialState != null)
                organismStatisticsPanel.TutorialState = tutorialState;
        }
    }

    /// <summary>
    ///   Needed for auto-evo prediction to be able to compare the new energy to the old energy
    /// </summary>
    [JsonProperty]
    public float? PreviousPlayerGatheredEnergy { get; set; }

    [JsonIgnore]
    public IEnumerable<OrganelleDefinition> PlacedUniqueOrganelles => editedMicrobeOrganelles
        .Where(p => p.Definition.Unique)
        .Select(p => p.Definition);

    [JsonIgnore]
    public override bool ShowFinishButtonWarning
    {
        get
        {
            if (base.ShowFinishButtonWarning)
                return true;

            if (IsNegativeAtpProduction())
                return true;

            if (HasIslands)
                return true;

            if (HasFinishedPendingEndosymbiosis)
                return true;

            return false;
        }
    }

    [JsonIgnore]
    public Func<string, bool>? ValidateNewCellTypeName { get; set; }

    /// <summary>
    ///   True when there are pending endosymbiosis actions. Only works after editor is fully initialized.
    /// </summary>
    [JsonIgnore]
    public bool HasFinishedPendingEndosymbiosis =>
        Editor.EditorReady && Editor.EditedBaseSpecies.Endosymbiosis.HasCompleteEndosymbiosis();

    protected override bool ForceHideHover => MicrobePreviewMode;

    private float CostMultiplier =>
        (IsMulticellularEditor ? Constants.MULTICELLULAR_EDITOR_COST_FACTOR : 1.0f) *
        Editor.CurrentGame.GameWorld.WorldSettings.MPMultiplier;

    public static void UpdateOrganelleDisplayerTransform(SceneDisplayer organelleModel, OrganelleTemplate organelle)
    {
        organelleModel.Transform = new Transform3D(
            new Basis(MathUtils.CreateRotationForOrganelle(1 * organelle.Orientation)),
            organelle.OrganelleModelPosition);

        organelleModel.Scale = organelle.Definition.GetUpgradesSizeModification(organelle.Upgrades);
    }

    /// <summary>
    ///   Updates the organelle model displayer to have the specified scene in it
    /// </summary>
    public static void UpdateOrganellePlaceHolderScene(SceneDisplayer organelleModel,
        LoadedSceneWithModelInfo displayScene, int renderPriority, List<ShaderMaterial> temporaryDataHolder)
    {
        organelleModel.Scene = displayScene.LoadedScene;

        temporaryDataHolder.Clear();
        if (!organelleModel.GetMaterial(temporaryDataHolder, displayScene.ModelPath))
        {
            GD.PrintErr("Failed to get material for editor / display cell to update render priority");
            return;
        }

        // To follow MicrobeRenderPrioritySystem this sets other than the first material to be -1 in priority
        bool first = true;

        foreach (var shaderMaterial in temporaryDataHolder)
        {
            if (first)
            {
                shaderMaterial.RenderPriority = renderPriority;
                first = false;
            }
            else
            {
                shaderMaterial.RenderPriority = renderPriority - 1;
            }
        }
    }

    public override void _Ready()
    {
        base._Ready();

        // This works only after this is attached to the scene tree
        // Hidden in the Godot editor to make selecting other things easier
        organelleUpgradeGUI.Visible = true;

        // TODO: make this setting persistent from somewhere
        // showGrowthOrderCoordinates.ButtonPressed = true;
        growthOrderGUI.ShowCoordinates = showGrowthOrderCoordinates.ButtonPressed;

        nucleus = SimulationParameters.Instance.GetOrganelleType("nucleus");
        bindingAgent = SimulationParameters.Instance.GetOrganelleType("bindingAgent");

        organelleSelectionButtonScene =
            GD.Load<PackedScene>("res://src/microbe_stage/editor/MicrobePartSelection.tscn");

        undiscoveredOrganellesScene =
            GD.Load<PackedScene>("res://src/microbe_stage/organelle_unlocks/UndiscoveredOrganellesButton.tscn");
        undiscoveredOrganellesTooltipScene =
            GD.Load<PackedScene>("res://src/microbe_stage/organelle_unlocks/UndiscoveredOrganellesTooltip.tscn");

        cytoplasm = SimulationParameters.Instance.GetOrganelleType("cytoplasm");

        SetupMicrobePartSelections();

        ApplySelectionMenuTab();
        RegisterTooltips();
    }

    public override void Init(ICellEditorData owningEditor, bool fresh)
    {
        base.Init(owningEditor, fresh);

        if (IsMulticellularEditor && ValidateNewCellTypeName == null)
        {
            throw new InvalidOperationException("The new cell type name validation callback needs to be set");
        }

        if (!IsMulticellularEditor)
        {
            behaviourEditor.Init(owningEditor, fresh);
            tolerancesEditor.Init(owningEditor, fresh);
        }
        else
        {
            // Endosymbiosis is not managed through this component in multicellular
            endosymbiosisButton.Visible = false;
        }

        // Visual simulation is needed very early when loading a save
        previewSimulation = new MicrobeVisualOnlySimulation();

        cellPreviewVisualsRoot = new Node3D
        {
            Name = "CellPreviewVisuals",
        };

        Editor.RootOfDynamicallySpawned.AddChild(cellPreviewVisualsRoot);

        previewSimulation.Init(cellPreviewVisualsRoot);

        var newLayout = new OrganelleLayout<OrganelleTemplate>(OnOrganelleAdded, OnOrganelleRemoved);

        if (fresh)
        {
            editedMicrobeOrganelles = newLayout;
        }
        else
        {
            // We assume that the loaded save layout did not have anything weird set for the callbacks as we
            // do this rather than use SaveApplyHelpers
            foreach (var editedMicrobeOrganelle in editedMicrobeOrganelles)
            {
                newLayout.AddFast(editedMicrobeOrganelle, hexTemporaryMemory, hexTemporaryMemory2);
            }

            editedMicrobeOrganelles = newLayout;

            if (Editor.EditedCellProperties != null)
            {
                UpdateGUIAfterLoadingSpecies(Editor.EditedBaseSpecies);
                CreateUndiscoveredOrganellesButtons();
                CreatePreviewMicrobeIfNeeded();
                UpdateArrow(false);
            }
            else
            {
                GD.Print("Loaded cell editor with no cell to edit set");
            }

            // Send info to the GUI about the organelle effectiveness in the current patch
            // When not loading a save, this is handled by OnEditorReady
            OnPatchDataReady();

            // Ensure the tolerance editor is set up to display current values when loading a save
            if (!IsMulticellularEditor)
            {
                tolerancesEditor.OnEditorSpeciesSetup(Editor.EditedBaseSpecies);
            }
        }

        if (IsMulticellularEditor)
        {
            componentBottomLeftButtons.HandleRandomSpeciesName = false;
            componentBottomLeftButtons.UseSpeciesNameValidation = false;

            // TODO: implement random cell type name generator
            componentBottomLeftButtons.ShowRandomizeButton = false;

            componentBottomLeftButtons.SetNamePlaceholder(Localization.Translate("CELL_TYPE_NAME"));

            autoEvoPredictionPanel.Visible = false;

            // In multicellular the body plan editor handles this
            behaviourTabButton.Visible = false;
            behaviourEditor.Visible = false;
            growthOrderTab.Visible = false;
            growthOrderTabButton.Visible = false;
            toleranceTab.Visible = false;
            toleranceTabButton.Visible = false;

            // Tolerances also should be implemented as overall ones for the entire species for multicellular
        }

        UpdateMicrobePartSelections();

        // After the "if multicellular check" so the tooltip cost factors are correct
        // on changing editor types, as the tooltip manager is persistent while the game is running
        UpdateMPCost();

        // Do this here as we know the editor and hence world settings have been initialised by now
        UpdateOrganelleLAWKSettings();

        organismStatisticsPanel.UpdateLightSelectionPanelVisibility(
            Editor.CurrentGame.GameWorld.WorldSettings.DayNightCycleEnabled && Editor.CurrentPatch.HasDayAndNight);

        ApplySymmetryForCurrentOrganelle();
    }

    public override void _Process(double delta)
    {
        if (cellPreviewVisualsRoot == null)
            throw new InvalidOperationException("This editor component is not initialized");

        base._Process(delta);

        if (!Visible)
            return;

        var debugOverlay = DebugOverlays.Instance;

        if (debugOverlay.PerformanceMetricsVisible)
        {
            var roughCount = Editor.RootOfDynamicallySpawned.GetChildCount();
            debugOverlay.ReportEntities(roughCount);
        }

        CheckRunningAutoEvoPrediction();
        CheckRunningSuggestion(delta);
        TriggerDelayedPredictionUpdateIfNeeded(delta);

        if (organelleDataDirty)
        {
            OnOrganellesChanged();
            organelleDataDirty = false;
        }

        // Process microbe visuals preview when it is visible
        if (cellPreviewVisualsRoot.Visible)
        {
            // Init being called is checked at the start of this method
            previewSimulation!.ProcessAll((float)delta);
        }

        // Update the growth order number positions each frame so that the camera moving doesn't get them out of sync
        // could do this with a dirty-flag approach for saving on performance but for now this is probably fine
        if (selectedSelectionMenuTab == SelectionMenuTab.GrowthOrder)
        {
            UpdateGrowthOrderNumbers();
        }

        if (refreshTolerancesWarnings)
        {
            refreshTolerancesWarnings = false;

            // Tolerances affect all the efficiency of all organelles, so we have to update this data here
            // the dataflow would be really hard to make sure no duplicate calls happen, so for now this just allows
            // duplicate calls
            CalculateOrganelleEffectivenessInCurrentPatch();

            // These are all also affected by the environmental tolerances
            CalculateEnergyAndCompoundBalance(editedMicrobeOrganelles.Organelles, Membrane);

            // Health is also affected
            UpdateStats();

            CalculateAndDisplayToleranceWarnings();
        }

        // Show the organelle that is about to be placed
        if (Editor.ShowHover && !MicrobePreviewMode)
        {
            GetMouseHex(out int q, out int r);

            OrganelleDefinition? shownOrganelle = null;

            var effectiveSymmetry = Symmetry;

            if (!CanCancelAction && ActiveActionName != null)
            {
                // Can place stuff at all?
                isPlacementProbablyValid =
                    IsValidPlacement(new OrganelleTemplate(GetOrganelleDefinition(ActiveActionName), new Hex(q, r),
                        placementRotation), true);

                shownOrganelle = SimulationParameters.Instance.GetOrganelleType(ActiveActionName);
            }
            else if (MovingPlacedHex != null)
            {
                isPlacementProbablyValid = IsMoveTargetValid(new Hex(q, r), placementRotation, MovingPlacedHex);
                shownOrganelle = MovingPlacedHex.Definition;

                if (!Settings.Instance.MoveOrganellesWithSymmetry)
                    effectiveSymmetry = HexEditorSymmetry.None;
            }
            else if (PendingEndosymbiontPlace != null)
            {
                shownOrganelle = PendingEndosymbiontPlace.PlacedOrganelle.Definition;
                isPlacementProbablyValid =
                    IsValidPlacement(new OrganelleTemplate(shownOrganelle, new Hex(q, r), placementRotation), false);

                effectiveSymmetry = HexEditorSymmetry.None;
            }

            if (shownOrganelle != null)
            {
                HashSet<(Hex Hex, int Orientation)> hoveredHexes = new();

                if (!componentBottomLeftButtons.SymmetryEnabled)
                    effectiveSymmetry = HexEditorSymmetry.None;

                RunWithSymmetry(q, r,
                    (finalQ, finalR, rotation) =>
                    {
                        RenderHighlightedOrganelle(finalQ, finalR, rotation, shownOrganelle, MovingPlacedHex?.Upgrades);
                        hoveredHexes.Add((new Hex(finalQ, finalR), rotation));
                    }, effectiveSymmetry);

                MouseHoverPositions = hoveredHexes.ToList();
            }
        }

        // Safety if the tutorial is disabled and some core GUI is disabled
        if (TutorialState is { Enabled: false })
        {
            if (!finishOrNextButton.Visible)
            {
                GD.Print("Restoring visibility of cell editor GUI that tutorial disabled");
                ShowStatisticsPanel(true);
                ShowConfirmButton(true);

                // Probably need to have this safety here to ensure that unintended things don't become visible in
                // multicellular
                if (!IsMulticellularEditor)
                {
                    ShowAutoEvoPredictionPanel(true);

                    growthOrderTabButton.Visible = true;
                    toleranceTabButton.Visible = true;
                    behaviourTabButton.Visible = true;
                }

                appearanceTabButton.Visible = true;
            }
        }
    }

    public override void OnEditorReady()
    {
        // As auto-evo results can modify the patch data, we only want to calculate the effectiveness of organelles in
        // the current patch once that data is ready (and whenever the selected patch changes of course)
        OnPatchDataReady();
    }

    [RunOnKeyDown("e_primary")]
    public override bool PerformPrimaryAction()
    {
        if (Visible && PendingEndosymbiontPlace != null)
        {
            GetMouseHex(out var q, out var r);
            return PerformEndosymbiosisPlace(q, r);
        }

        return base.PerformPrimaryAction();
    }

    [RunOnKeyDown("e_cancel_current_action", Priority = 1)]
    public override bool CancelCurrentAction()
    {
        if (Visible && PendingEndosymbiontPlace != null)
        {
            OnCurrentActionCanceled();
            return true;
        }

        return base.CancelCurrentAction();
    }

    public override void OnEditorSpeciesSetup(Species species)
    {
        base.OnEditorSpeciesSetup(species);

        // For multicellular the cell editor is initialized before a cell type to edit is selected so we skip
        // the logic here the first time this is called too early
        var properties = Editor.EditedCellProperties;
        if (properties == null && IsMulticellularEditor)
            return;

        if (IsMulticellularEditor)
        {
            // Prepare for second use in multicellular editor
            editedMicrobeOrganelles.Clear();
        }
        else if (editedMicrobeOrganelles.Count > 0)
        {
            GD.PrintErr("Reusing cell editor without marking it for multicellular is not meant to be done");
        }

        // We set these here to make sure these are ready in the organelle add callbacks (even though currently
        // that just marks things dirty, and we update our stats on the next _Process call)
        Membrane = properties!.MembraneType;
        Rigidity = properties.MembraneRigidity;
        Colour = properties.Colour;

        if (!IsMulticellularEditor)
        {
            behaviourEditor.OnEditorSpeciesSetup(species);
            tolerancesEditor.OnEditorSpeciesSetup(species);
        }

        // Get the species organelles to be edited. This also updates the placeholder hexes
        foreach (var organelle in properties.Organelles.Organelles)
        {
            editedMicrobeOrganelles.AddFast((OrganelleTemplate)organelle.Clone(), hexTemporaryMemory,
                hexTemporaryMemory2);
        }

        newName = properties.FormattedName;

        // This needs to be calculated here, otherwise ATP-related unlock conditions would
        // get null as the ATP balance
        CalculateEnergyAndCompoundBalance(properties.Organelles.Organelles, properties.MembraneType,
            Editor.CurrentPatch.Biome);

        UpdateOrganelleUnlockTooltips(true);

        UpdateGUIAfterLoadingSpecies(Editor.EditedBaseSpecies);

        // Set up the display cell
        CreatePreviewMicrobeIfNeeded();

        UpdateArrow(false);

        if (!IsMulticellularEditor)
        {
            // Make sure initial tolerance warnings are shown
            OnTolerancesChanged(tolerancesEditor.CurrentTolerances);
        }
    }

    public override void OnFinishEditing()
    {
        OnFinishEditing(true);
    }

    public void OnFinishEditing(bool shouldUpdatePosition)
    {
        var editedSpecies = Editor.EditedBaseSpecies;
        var editedProperties = Editor.EditedCellProperties;

        if (editedProperties == null)
        {
            GD.Print("Cell editor skip applying changes as no target cell properties set");
            return;
        }

        // Apply changes to the species organelles
        // It is easiest to just replace all
        editedProperties.Organelles.Clear();

        // Even in a multicellular context, it should always be safe to apply the organelle growth order
        foreach (var organelle in growthOrderGUI.ApplyOrderingToItems(editedMicrobeOrganelles.Organelles))
        {
            var organelleToAdd = (OrganelleTemplate)organelle.Clone();
            editedProperties.Organelles.AddFast(organelleToAdd, hexTemporaryMemory, hexTemporaryMemory2);
        }

        if (shouldUpdatePosition)
            editedProperties.RepositionToOrigin();

        // Update bacteria status
        editedProperties.IsBacteria = !HasNucleus;

        editedProperties.UpdateNameIfValid(newName);

        // Update membrane
        editedProperties.MembraneType = Membrane;
        editedProperties.Colour = Colour;
        editedProperties.MembraneRigidity = Rigidity;

        if (!IsMulticellularEditor)
        {
            GD.Print("MicrobeEditor: updated organelles for species: ", editedSpecies.FormattedName);

            behaviourEditor.OnFinishEditing();
            tolerancesEditor.OnFinishEditing();

            // When this is the primary editor of the species data, this must refresh the species data properties that
            // depend on being edited
            editedSpecies.OnEdited();
        }
        else
        {
            GD.Print("MicrobeEditor: updated organelles for cell: ", editedProperties.FormattedName);
        }
    }

    public override void SetEditorWorldTabSpecificObjectVisibility(bool shown)
    {
        if (cellPreviewVisualsRoot == null)
            throw new InvalidOperationException("This component is not initialized yet");

        base.SetEditorWorldTabSpecificObjectVisibility(shown && !MicrobePreviewMode);

        cellPreviewVisualsRoot.Visible = shown && MicrobePreviewMode;
    }

    public override bool CanFinishEditing(IEnumerable<EditorUserOverride> userOverrides)
    {
        var editorUserOverrides = userOverrides.ToList();
        if (!base.CanFinishEditing(editorUserOverrides))
            return false;

        // Disallow exiting if the confirmation button hasn't been enabled yet (by the tutorial). This is just a safety
        // check against accidentally skipping the tutorial
        if (tutorialState is { Enabled: true } && !finishOrNextButton.Visible)
        {
            GD.Print("Disallowing exit of cell editor as tutorial is still active (and confirm is hidden)");
            return false;
        }

        // Show a warning if the editor has an endosymbiosis that should be finished
        if (HasFinishedPendingEndosymbiosis && !editorUserOverrides.Contains(EditorUserOverride.EndosymbiosisPending))
        {
            pendingEndosymbiosisPopup.PopupCenteredShrink();
            return false;
        }

        // Show a warning popup if trying to exit with negative atp production
        // Not shown in multicellular as the popup would happen in kind of weird place
        if (!IsMulticellularEditor && IsNegativeAtpProduction() &&
            !editorUserOverrides.Contains(EditorUserOverride.NotProducingEnoughATP))
        {
            negativeAtpPopup.PopupCenteredShrink();
            return false;
        }

        // This is triggered when no changes have been made. A more accurate way would be to check the action history
        // for any undoable action, but that isn't accessible here currently, so this is probably good enough.
        if (Editor.MutationPoints == Constants.BASE_MUTATION_POINTS)
        {
            var tutorial = Editor.CurrentGame.TutorialState;

            // In the multicellular editor the cell editor might not be visible, so preventing exiting the editor
            // without explanation is not a good idea, so that's why this check is here
            if (tutorial.Enabled && !IsMulticellularEditor)
            {
                tutorial.SendEvent(TutorialEventType.MicrobeEditorNoChangesMade, EventArgs.Empty, this);

                if (tutorial.TutorialActive())
                    return false;
            }
        }

        return true;
    }

    public void HideGUIElementsForInitialTutorial()
    {
        organismStatisticsPanel.Visible = false;
        finishOrNextButton.Visible = false;
        HideAutoEvoPredictionForTutorial();

        // Don't show the most advanced tabs
        growthOrderTabButton.Visible = false;
        toleranceTabButton.Visible = false;

        // And don't show these yet
        behaviourTabButton.Visible = false;
        appearanceTabButton.Visible = false;
    }

    public void HideAdvancedTabs()
    {
        // Hide the most advanced tabs
        growthOrderTabButton.Visible = false;
        toleranceTabButton.Visible = false;
    }

    public bool AreAdvancedTabsVisible()
    {
        return growthOrderTabButton.Visible || toleranceTabButton.Visible;
    }

    public void HideAutoEvoPredictionForTutorial()
    {
        autoEvoPredictionPanel.Visible = false;
    }

    public void ShowStatisticsPanel(bool animate)
    {
        organismStatisticsPanel.Visible = true;

        if (!animate)
            return;

        // Due to anchor positioning, this needs an animation defined with code to work correctly; otherwise the final
        // position will not be correct
        var tween = CreateTween();

        var targetPosition = rightPanel.Position;
        tween.SetEase(Tween.EaseType.InOut);
        tween.SetTrans(Tween.TransitionType.Expo);
        tween.TweenProperty(rightPanel, "position", targetPosition, 0.5)
            .From(targetPosition + new Vector2(rightPanel.Size.X + 5, 0));
    }

    public void ShowBasicEditingTabs()
    {
        appearanceTabButton.Visible = true;

        if (!IsMulticellularEditor)
            behaviourTabButton.Visible = true;
    }

    public void ShowConfirmButton(bool animate)
    {
        finishOrNextButton.Visible = true;

        if (!animate)
            return;

        var tween = CreateTween();

        var targetPosition = bottomRightPanel.Position;
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(bottomRightPanel, "position", targetPosition, 0.4)
            .From(targetPosition + new Vector2(0, bottomRightPanel.Size.Y + 5));
    }

    public void ShowAutoEvoPredictionPanel(bool animate)
    {
        autoEvoPredictionPanel.Visible = true;

        if (!animate)
            return;

        tutorialAnimationPlayer.Play("ShowAutoEvoPrediction");
    }

    /// <summary>
    ///   Allows access to the latest edited organelles by this component. Shouldn't be modified but just read.
    /// </summary>
    /// <returns>Access to the latest organelle edits</returns>
    public OrganelleLayout<OrganelleTemplate> GetLatestEditedOrganelles()
    {
        return editedMicrobeOrganelles;
    }

    /// <summary>
    ///   Report that the current patch used in the editor has changed
    /// </summary>
    /// <param name="patch">The patch that is set</param>
    public void OnCurrentPatchUpdated(Patch patch)
    {
        _ = patch;

        organismStatisticsPanel.UpdateLightSelectionPanelVisibility(
            Editor.CurrentGame.GameWorld.WorldSettings.DayNightCycleEnabled && Editor.CurrentPatch.HasDayAndNight);

        CalculateOrganelleEffectivenessInCurrentPatch();
        UpdatePatchDependentBalanceData();

        if (!IsMulticellularEditor)
        {
            // Refresh tolerances data for the new patch
            tolerancesEditor.OnDataTolerancesDependOnChanged();
            OnTolerancesChanged(tolerancesEditor.CurrentTolerances);
        }

        // Redo suggestion calculations as they could depend on the patch data (though at the time of writing this is
        // not really changing)
        autoEvoPredictionDirty = true;
        suggestionDirty = true;
    }

    /// <summary>
    ///   Call when behaviour data changes, this re-triggers auto-evo prediction to use the updated values
    /// </summary>
    /// <param name="behaviourData">New behaviour data</param>
    public void OnBehaviourDataUpdated(BehaviourDictionary behaviourData)
    {
        overwriteBehaviourForCalculations = behaviourData;

        autoEvoPredictionDirty = true;
        suggestionDirty = true;
    }

    /// <summary>
    ///   Call when tolerance data changes, re-triggers simulations and updates the GUI warnings
    /// </summary>
    /// <param name="newTolerances">New tolerance data</param>
    public void OnTolerancesChanged(EnvironmentalTolerances newTolerances)
    {
        autoEvoPredictionDirty = true;
        suggestionDirty = true;

        // Need to show new tolerances warnings (and refresh a few other things)
        refreshTolerancesWarnings = true;
    }

    public void UpdatePatchDependentBalanceData()
    {
        // Skip if opened in the multicellular editor
        if (IsMulticellularEditor && editedMicrobeOrganelles.Organelles.Count < 1)
            return;

        organismStatisticsPanel.UpdateLightSelectionPanelVisibility(
            Editor.CurrentGame.GameWorld.WorldSettings.DayNightCycleEnabled && Editor.CurrentPatch.HasDayAndNight);

        // Calculate and send energy balance and compound balance to the GUI
        CalculateEnergyAndCompoundBalance(editedMicrobeOrganelles.Organelles, Membrane);

        UpdateOrganelleUnlockTooltips(false);
    }

    /// <summary>
    ///   Calculates the effectiveness of organelles in the current patch (actually the editor biome conditions which
    ///   may have additional modifiers applied)
    /// </summary>
    public void CalculateOrganelleEffectivenessInCurrentPatch()
    {
        var organelles = SimulationParameters.Instance.GetAllOrganelles();

        var result =
            ProcessSystem.ComputeOrganelleProcessEfficiencies(organelles, Editor.CurrentPatch.Biome,
                CalculateLatestTolerances(), CompoundAmountType.Current);

        UpdateOrganelleEfficiencies(result);
    }

    /// <summary>
    ///   Wipes clean the current cell.
    /// </summary>
    public void CreateNewMicrobe()
    {
        if (!Editor.FreeBuilding)
            throw new InvalidOperationException("can't reset cell when not freebuilding");

        var oldEditedMicrobeOrganelles = new OrganelleLayout<OrganelleTemplate>();
        var oldMembrane = Membrane;

        foreach (var organelle in editedMicrobeOrganelles)
        {
            oldEditedMicrobeOrganelles.AddFast(organelle, hexTemporaryMemory, hexTemporaryMemory2);
        }

        NewMicrobeActionData data;
        if (IsMulticellularEditor)
        {
            // Behaviour editor is not used in multicellular
            data = new NewMicrobeActionData(oldEditedMicrobeOrganelles, oldMembrane, Rigidity, Colour, null, null);
        }
        else
        {
            data = new NewMicrobeActionData(oldEditedMicrobeOrganelles, oldMembrane, Rigidity, Colour,
                behaviourEditor.Behaviour ?? throw new Exception("Behaviour not initialized"),
                tolerancesEditor.CurrentTolerances);
        }

        var action =
            new SingleEditorAction<NewMicrobeActionData>(DoNewMicrobeAction, UndoNewMicrobeAction, data);

        Editor.EnqueueAction(action);
    }

    public void OnMembraneSelected(string membraneName)
    {
        var membrane = SimulationParameters.Instance.GetMembrane(membraneName);

        if (Membrane.Equals(membrane))
            return;

        var action = new SingleEditorAction<MembraneActionData>(DoMembraneChangeAction, UndoMembraneChangeAction,
            new MembraneActionData(Membrane, membrane)
            {
                CostMultiplier = CostMultiplier,
            });

        Editor.EnqueueAction(action);

        // In case the action failed, we need to make sure the membrane buttons are updated properly
        UpdateMembraneButtons(Membrane.InternalName);
    }

    public void OnRigidityChanged(int desiredRigidity)
    {
        int previousRigidity = (int)Math.Round(Rigidity * Constants.MEMBRANE_RIGIDITY_SLIDER_TO_VALUE_RATIO);

        if (CanCancelAction)
        {
            Editor.OnActionBlockedWhileMoving();
            UpdateRigiditySlider(previousRigidity);
            return;
        }

        if (previousRigidity == desiredRigidity)
            return;

        var costPerStep = Math.Min(Constants.MEMBRANE_RIGIDITY_COST_PER_STEP * CostMultiplier, 100);

        var data = new RigidityActionData(desiredRigidity / Constants.MEMBRANE_RIGIDITY_SLIDER_TO_VALUE_RATIO, Rigidity)
        {
            CostMultiplier = CostMultiplier,
        };

        // In some cases "theoreticalCost" might get rounded improperly
        var theoreticalCost = Editor.WhatWouldActionsCost(new[] { data });

        // Removed cast to int here doesn't solve https://github.com/Revolutionary-Games/Thrive/issues/5821
        var cost = Math.Ceiling(Math.Ceiling(theoreticalCost / costPerStep) * costPerStep);

        // Cases where mutation points are equal 0 are handled below in the next "if" statement
        if (cost > Editor.MutationPoints && Editor.MutationPoints > 0)
        {
            int stepsToCutOff = (int)Math.Ceiling((cost - Editor.MutationPoints) / costPerStep);
            data.NewRigidity -= (desiredRigidity - previousRigidity > 0 ? 1 : -1) * stepsToCutOff /
                Constants.MEMBRANE_RIGIDITY_SLIDER_TO_VALUE_RATIO;

            // Action is enqueued or canceled here, so we don't need to go on.
            UpdateRigiditySlider((int)Math.Round(data.NewRigidity * Constants.MEMBRANE_RIGIDITY_SLIDER_TO_VALUE_RATIO));
            return;
        }

        // Make sure that if there are no mutation points, the player cannot drag the slider
        // when the cost is rounded to zero
        if (theoreticalCost >= 0 && (Editor.MutationPoints - cost <= 0 || costPerStep > Editor.MutationPoints))
        {
            UpdateRigiditySlider(previousRigidity);
            return;
        }

        var action = new SingleEditorAction<RigidityActionData>(DoRigidityChangeAction, UndoRigidityChangeAction, data);

        Editor.EnqueueAction(action);
    }

    /// <summary>
    ///   Show options for the organelle under the cursor
    /// </summary>
    /// <returns>True when this was able to do something and consume the keypress</returns>
    [RunOnKeyDown("e_secondary")]
    public bool ShowOrganelleOptions()
    {
        // Need to prevent this from running when not visible to not conflict in an editor with multiple tabs
        if (MicrobePreviewMode || !Visible)
            return false;

        // Can't open organelle popup menu while moving something
        if (CanCancelAction)
        {
            Editor.OnActionBlockedWhileMoving();
            return true;
        }

        GetMouseHex(out int q, out int r);

        // This is a list to preserve order, Distinct is used later to ensure no duplicate organelles are added
        var organelles = new List<OrganelleTemplate>();

        RunWithSymmetry(q, r, (symmetryQ, symmetryR, _) =>
        {
            var organelle = editedMicrobeOrganelles.GetElementAt(new Hex(symmetryQ, symmetryR), hexTemporaryMemory);

            if (organelle != null)
                organelles.Add(organelle);
        });

        if (organelles.Count < 1)
            return true;

        ShowOrganelleMenu(organelles.Distinct());
        return true;
    }

    public override void OnValidAction(IEnumerable<CombinableActionData> actions)
    {
        var endosymbiontPlace = typeof(EndosymbiontPlaceActionData);

        // Most likely better to enumerate multiple times rather than allocate temporary memory
        // ReSharper disable PossibleMultipleEnumeration
        foreach (var data in actions)
        {
            var type = data.GetType();
            if (type.IsAssignableToGenericType(endosymbiontPlace))
            {
                PlayHexPlacementSound();
                break;
            }
        }

        base.OnValidAction(actions);

        // ReSharper restore PossibleMultipleEnumeration
    }

    public float CalculateSpeed()
    {
        return MicrobeInternalCalculations.CalculateSpeed(editedMicrobeOrganelles.Organelles, Membrane, Rigidity,
            !HasNucleus);
    }

    public float CalculateRotationSpeed()
    {
        return MicrobeInternalCalculations.CalculateRotationSpeed(editedMicrobeOrganelles.Organelles);
    }

    public float CalculateHitpoints()
    {
        var maxHitpoints = Membrane.Hitpoints + Rigidity * Constants.MEMBRANE_RIGIDITY_HITPOINTS_MODIFIER;

        // Tolerances affect health
        maxHitpoints *= CalculateLatestTolerances().HealthModifier;

        return maxHitpoints;
    }

    public Dictionary<Compound, float> GetAdditionalCapacities(out float nominalCapacity)
    {
        return MicrobeInternalCalculations.GetTotalSpecificCapacity(editedMicrobeOrganelles, out nominalCapacity);
    }

    public float CalculateTotalDigestionSpeed()
    {
        return MicrobeInternalCalculations.CalculateTotalDigestionSpeed(editedMicrobeOrganelles);
    }

    public Dictionary<Enzyme, float> CalculateDigestionEfficiencies()
    {
        return MicrobeInternalCalculations.CalculateDigestionEfficiencies(editedMicrobeOrganelles);
    }

    public (int AmmoniaCost, int PhosphatesCost) CalculateOrganellesCosts()
    {
        return MicrobeInternalCalculations.CalculateOrganellesCosts(editedMicrobeOrganelles);
    }

    public override void OnLightLevelChanged(float dayLightFraction)
    {
        UpdateVisualLightLevel(dayLightFraction, Editor.CurrentPatch);

        CalculateOrganelleEffectivenessInCurrentPatch();
        UpdatePatchDependentBalanceData();
    }

    public bool ApplyOrganelleUpgrade(OrganelleUpgradeActionData actionData)
    {
        actionData.CostMultiplier = CostMultiplier;

        return EnqueueAction(new CombinedEditorAction(new SingleEditorAction<OrganelleUpgradeActionData>(
            DoOrganelleUpgradeAction, UndoOrganelleUpgradeAction,
            actionData)));
    }

    protected override double CalculateCurrentActionCost()
    {
        if (string.IsNullOrEmpty(ActiveActionName) || !Editor.ShowHover)
            return 0;

        // Endosymbiosis placement is free
        if (PendingEndosymbiontPlace != null)
            return 0;

        var organelleDefinition = SimulationParameters.Instance.GetOrganelleType(ActiveActionName!);

        // Calculated in this order to be consistent with placing unique organelles
        var cost = (int)Math.Min(organelleDefinition.MPCost * CostMultiplier, 100);

        if (MouseHoverPositions == null)
            return cost * Symmetry.PositionCount();

        var positions = MouseHoverPositions.ToList();

        var organelleTemplates = positions
            .Select(h => new OrganelleTemplate(organelleDefinition, h.Hex, h.Orientation)).ToList();

        CombinedEditorAction moveOccupancies;

        if (MovingPlacedHex == null)
        {
            moveOccupancies = GetMultiActionWithOccupancies(positions, organelleTemplates, false);
        }
        else
        {
            moveOccupancies =
                GetMultiActionWithOccupancies(positions.Take(1).ToList(),
                    new List<OrganelleTemplate> { MovingPlacedHex }, true);
        }

        return Editor.WhatWouldActionsCost(moveOccupancies.Data);
    }

    protected override void PerformActiveAction()
    {
        var organelle = ActiveActionName!;

        if (AddOrganelle(organelle))
        {
            // Only trigger tutorial if an organelle was really placed
            TutorialState?.SendEvent(TutorialEventType.MicrobeEditorOrganellePlaced,
                new OrganellePlacedEventArgs(GetOrganelleDefinition(organelle)), this);
        }
    }

    protected override void PerformMove(int q, int r)
    {
        if (!MoveOrganelle(MovingPlacedHex!, new Hex(q, r),
                placementRotation))
        {
            Editor.OnInvalidAction();
        }
    }

    protected override void OnPendingActionWillSucceed()
    {
        PendingEndosymbiontPlace = null;

        base.OnPendingActionWillSucceed();

        // Update rigidity slider in case it was disabled
        // TODO: could come up with a bit nicer design here
        int intRigidity = (int)Math.Round(Rigidity * Constants.MEMBRANE_RIGIDITY_SLIDER_TO_VALUE_RATIO);
        UpdateRigiditySlider(intRigidity);
    }

    protected override bool IsMoveTargetValid(Hex position, int rotation, OrganelleTemplate organelle)
    {
        return editedMicrobeOrganelles.CanPlace(organelle.Definition, position, rotation, hexTemporaryMemory, false);
    }

    protected override void OnCurrentActionCanceled()
    {
        if (MovingPlacedHex != null)
        {
            editedMicrobeOrganelles.AddFast(MovingPlacedHex, hexTemporaryMemory, hexTemporaryMemory2);
            MovingPlacedHex = null;
        }

        PendingEndosymbiontPlace = null;

        base.OnCurrentActionCanceled();
    }

    protected override bool DoesActionEndInProgressAction(CombinedEditorAction action)
    {
        if (PendingEndosymbiontPlace != null)
        {
            return action.Data.Any(d => d is EndosymbiontPlaceActionData);
        }

        return base.DoesActionEndInProgressAction(action);
    }

    protected override void OnMoveActionStarted()
    {
        editedMicrobeOrganelles.Remove(MovingPlacedHex!);
    }

    protected override OrganelleTemplate? GetHexAt(Hex position)
    {
        return editedMicrobeOrganelles.GetElementAt(position, hexTemporaryMemory);
    }

    protected override EditorAction? TryCreateRemoveHexAtAction(Hex location, ref int alreadyDeleted)
    {
        var organelleHere = editedMicrobeOrganelles.GetElementAt(location, hexTemporaryMemory);
        if (organelleHere == null)
            return null;

        // Don't allow deletion of nucleus or the last organelle
        if (organelleHere.Definition == nucleus || MicrobeSize - alreadyDeleted < 2)
            return null;

        // In multicellular binding agents can't be removed
        if (IsMulticellularEditor && organelleHere.Definition == bindingAgent)
            return null;

        ++alreadyDeleted;
        return new SingleEditorAction<OrganelleRemoveActionData>(DoOrganelleRemoveAction, UndoOrganelleRemoveAction,
            new OrganelleRemoveActionData(organelleHere)
            {
                CostMultiplier = CostMultiplier,
            });
    }

    protected void UpdateOrganellePlaceHolderScene(SceneDisplayer organelleModel,
        LoadedSceneWithModelInfo displayScene, int renderPriority)
    {
        UpdateOrganellePlaceHolderScene(organelleModel, displayScene, renderPriority, temporaryDisplayerFetchList);
    }

    protected override float CalculateEditorArrowZPosition()
    {
        // The calculation falls back to 0 if there are no hexes found in the middle 3 rows
        var highestPointInMiddleRows = 0.0f;

        // Iterate through all organelles
        foreach (var organelle in editedMicrobeOrganelles)
        {
            // Iterate through all hexes
            foreach (var relativeHex in organelle.Definition.Hexes)
            {
                var absoluteHex = relativeHex + organelle.Position;

                // Only consider the middle 3 rows
                if (absoluteHex.Q is < -1 or > 1)
                    continue;

                var cartesian = Hex.AxialToCartesian(absoluteHex);

                // Get the min z-axis (highest point in the editor)
                highestPointInMiddleRows = MathF.Min(highestPointInMiddleRows, cartesian.Z);
            }
        }

        return highestPointInMiddleRows;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            previewSimulation?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    ///   Called once patch data is ready for initial read
    /// </summary>
    private void OnPatchDataReady()
    {
        CalculateOrganelleEffectivenessInCurrentPatch();
        UpdatePatchDependentBalanceData();
    }

    private bool PerformEndosymbiosisPlace(int q, int r)
    {
        if (PendingEndosymbiontPlace == null)
        {
            GD.PrintErr("No endosymbiosis place in progress, there should be at this point");
            Editor.OnInvalidAction();
            return false;
        }

        PendingEndosymbiontPlace.PlacementLocation = new Hex(q, r);
        PendingEndosymbiontPlace.PlacementRotation = placementRotation;

        PendingEndosymbiontPlace.PlacedOrganelle.Orientation = placementRotation;
        PendingEndosymbiontPlace.PlacedOrganelle.Position = new Hex(q, r);

        // Before finalizing the data, make sure it can be placed at the current position
        if (!IsValidPlacement(PendingEndosymbiontPlace.PlacedOrganelle, false))
        {
            Editor.OnInvalidAction();
            return false;
        }

        var action = new CombinedEditorAction(new SingleEditorAction<EndosymbiontPlaceActionData>(
            DoEndosymbiontPlaceAction, UndoEndosymbiontPlaceAction, PendingEndosymbiontPlace));

        EnqueueAction(action);

        return true;
    }

    private bool CreatePreviewMicrobeIfNeeded()
    {
        if (previewSimulation == null)
            throw new InvalidOperationException("Component needs to be initialized first");

        if (previewMicrobe.IsAlive && previewMicrobeSpecies != null)
            return false;

        if (cellPreviewVisualsRoot == null)
        {
            throw new InvalidOperationException("Editor component not initialized yet (cell visuals root missing)");
        }

        previewMicrobeSpecies = new MicrobeSpecies(Editor.EditedBaseSpecies,
            Editor.EditedCellProperties ??
            throw new InvalidOperationException("can't setup preview before cell properties are known"),
            hexTemporaryMemory, hexTemporaryMemory2)
        {
            // Force large normal size (instead of showing bacteria as smaller scale than the editor hexes)
            IsBacteria = false,
        };

        previewMicrobe = previewSimulation.CreateVisualisationMicrobe(previewMicrobeSpecies);

        // Set its initial visibility
        cellPreviewVisualsRoot.Visible = MicrobePreviewMode;

        return true;
    }

    /// <summary>
    ///   Updates the membrane and organelle placement of the preview cell.
    /// </summary>
    private void UpdateCellVisualization()
    {
        if (previewMicrobeSpecies == null)
            return;

        // Don't redo the preview cell when not in the preview mode to avoid unnecessary lags
        if (!MicrobePreviewMode || !microbeVisualizationOrganellePositionsAreDirty)
            return;

        CopyEditedPropertiesToSpecies(previewMicrobeSpecies);

        // Intentionally force it to not be bacteria to show it at full size
        previewMicrobeSpecies.IsBacteria = false;

        // This is now just for applying changes in the species to the preview cell
        previewSimulation!.ApplyNewVisualisationMicrobeSpecies(previewMicrobe, previewMicrobeSpecies);

        microbeVisualizationOrganellePositionsAreDirty = false;
    }

    private bool HasOrganelle(OrganelleDefinition organelleDefinition)
    {
        return editedMicrobeOrganelles.Organelles.Any(o => o.Definition == organelleDefinition);
    }

    private void UpdateRigiditySlider(int value)
    {
        rigiditySlider.Value = value;
        SetRigiditySliderTooltip(value);
    }

    private void ShowOrganelleMenu(IEnumerable<OrganelleTemplate> selectedOrganelles)
    {
        var organelles = selectedOrganelles.ToList();
        organelleMenu.SelectedOrganelles = organelles;
        organelleMenu.CostMultiplier = CostMultiplier;
        organelleMenu.GetActionPrice = Editor.WhatWouldActionsCost;
        organelleMenu.ShowPopup = true;

        var count = organelles.Count;

        // Disable delete for nucleus or the last organelle.
        bool attemptingNucleusDelete = organelles.Any(o => o.Definition == nucleus);
        if (MicrobeSize <= count || attemptingNucleusDelete)
        {
            organelleMenu.EnableDeleteOption = false;

            organelleMenu.DeleteOptionTooltip = attemptingNucleusDelete ?
                Localization.Translate("NUCLEUS_DELETE_OPTION_DISABLED_TOOLTIP") :
                Localization.Translate("LAST_ORGANELLE_DELETE_OPTION_DISABLED_TOOLTIP");
        }
        else
        {
            // Additionally in multicellular binding agents can't be removed
            if (IsMulticellularEditor && organelles.Any(o => o.Definition == bindingAgent))
            {
                organelleMenu.EnableDeleteOption = false;
            }
            else
            {
                organelleMenu.EnableDeleteOption = true;
            }

            organelleMenu.DeleteOptionTooltip = string.Empty;
        }

        // Move enabled only when microbe has more than one organelle
        organelleMenu.EnableMoveOption = MicrobeSize > 1;

        // Modify / upgrade possible when defined on the primary organelle definition
        if (count > 0 && IsUpgradingPossibleFor(organelles.First().Definition))
        {
            organelleMenu.EnableModifyOption = true;
        }
        else
        {
            organelleMenu.EnableModifyOption = false;
        }
    }

    private bool IsUpgradingPossibleFor(OrganelleDefinition organelleDefinition)
    {
        return !string.IsNullOrEmpty(organelleDefinition.UpgradeGUI) || organelleDefinition.AvailableUpgrades.Count > 0;
    }

    /// <summary>
    ///   Returns a list with hex, orientation, the organelle and whether or not this hex is already occupied by a
    ///   higher-ranked organelle.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     An organelle is ranked higher if it costs more MP.
    ///   </para>
    ///   <para>
    ///     TODO: figure out why the OrganelleTemplate in the tuples used here can be null and simplify the logic if
    ///     it's possible
    ///   </para>
    /// </remarks>
    private IEnumerable<(Hex Hex, OrganelleTemplate Organelle, int Orientation, bool Occupied)> GetOccupancies(
        List<(Hex Hex, int Orientation)> hexes, List<OrganelleTemplate> organelles)
    {
        var organellePositions = new List<(Hex Hex, OrganelleTemplate? Organelle, int Orientation, bool Occupied)>();
        for (var i = 0; i < hexes.Count; ++i)
        {
            var (hex, orientation) = hexes[i];
            var organelle = organelles[i];
            var oldOrganelle = organellePositions.FirstOrDefault(p => p.Hex == hex);
            var occupied = false;
            if (oldOrganelle != default)
            {
                if (organelle.Definition.MPCost > oldOrganelle.Organelle?.Definition.MPCost)
                {
                    organellePositions.Remove(oldOrganelle);
                    oldOrganelle.Occupied = true;
                    organellePositions.Add(oldOrganelle);
                }
                else
                {
                    occupied = true;
                }
            }

            organellePositions.Add((hex, organelle, orientation, occupied));
        }

        return organellePositions.Where(t => t.Organelle != null)!;
    }

    private CombinedEditorAction GetMultiActionWithOccupancies(List<(Hex Hex, int Orientation)> hexes,
        List<OrganelleTemplate> organelles, bool moving)
    {
        var actions = new List<EditorAction>();
        foreach (var (hex, organelle, orientation, occupied) in GetOccupancies(hexes, organelles))
        {
            EditorAction action;
            if (occupied)
            {
                var data = new OrganelleRemoveActionData(organelle)
                {
                    GotReplaced = organelle.Definition.InternalName == cytoplasm.InternalName,
                    CostMultiplier = CostMultiplier,
                };
                action = new SingleEditorAction<OrganelleRemoveActionData>(DoOrganelleRemoveAction,
                    UndoOrganelleRemoveAction, data);
            }
            else
            {
                if (moving)
                {
                    var data = new OrganelleMoveActionData(organelle, organelle.Position, hex, organelle.Orientation,
                        orientation)
                    {
                        CostMultiplier = CostMultiplier,
                    };
                    action = new SingleEditorAction<OrganelleMoveActionData>(DoOrganelleMoveAction,
                        UndoOrganelleMoveAction, data);
                }
                else
                {
                    var data = new OrganellePlacementActionData(organelle, hex, orientation)
                    {
                        CostMultiplier = CostMultiplier,
                    };

                    var replacedHexes = organelle.RotatedHexes
                        .Select(h => editedMicrobeOrganelles.GetElementAt(hex + h, hexTemporaryMemory)).WhereNotNull()
                        .ToList();

                    if (replacedHexes.Count > 0)
                        data.ReplacedCytoplasm = replacedHexes;

                    action = new SingleEditorAction<OrganellePlacementActionData>(DoOrganellePlaceAction,
                        UndoOrganellePlaceAction, data);
                }
            }

            actions.Add(action);
        }

        return new CombinedEditorAction(actions);
    }

    private IEnumerable<OrganelleTemplate> GetReplacedCytoplasm(IEnumerable<OrganelleTemplate> organelles)
    {
        foreach (var templateHex in organelles
                     .Where(o => o.Definition.InternalName != cytoplasm.InternalName)
                     .SelectMany(o => o.RotatedHexes.Select(h => h + o.Position)))
        {
            var existingOrganelle = editedMicrobeOrganelles.GetElementAt(templateHex, hexTemporaryMemory);

            if (existingOrganelle != null && existingOrganelle.Definition.InternalName == cytoplasm.InternalName)
            {
                yield return existingOrganelle;
            }
        }
    }

    private IEnumerable<OrganelleRemoveActionData> GetReplacedCytoplasmRemoveActionData(
        IEnumerable<OrganelleTemplate> organelles)
    {
        return GetReplacedCytoplasm(organelles)
            .Select(o => new OrganelleRemoveActionData(o)
            {
                GotReplaced = true,
                CostMultiplier = CostMultiplier,
            });
    }

    private IEnumerable<SingleEditorAction<OrganelleRemoveActionData>> GetReplacedCytoplasmRemoveAction(
        IEnumerable<OrganelleTemplate> organelles)
    {
        var replacedCytoplasmData = GetReplacedCytoplasmRemoveActionData(organelles);
        return replacedCytoplasmData.Select(o =>
            new SingleEditorAction<OrganelleRemoveActionData>(DoOrganelleRemoveAction, UndoOrganelleRemoveAction, o));
    }

    /// <summary>
    ///   Immediately start a new auto-evo prediction run. For actions that can trigger quickly in a sequence prefer
    ///   setting <see cref="autoEvoPredictionDirty"/> to false to prevent rapid restarts of the prediction.
    /// </summary>
    private void StartAutoEvoPrediction()
    {
        // For now disabled in the multicellular editor as the microbe logic being used there doesn't make sense
        if (IsMulticellularEditor)
            return;

        // The first prediction can be made only after population numbers from previous run are applied
        // so this is just here to guard against that potential programming mistake that may happen when code is
        // changed
        if (!Editor.EditorReady)
        {
            GD.PrintErr("Can't start auto-evo prediction before editor is ready");
            return;
        }

        // Note that in rare cases the auto-evo run doesn't manage to stop before we edit the cached species object,
        // which may cause occasional background task errors
        CancelPreviousAutoEvoPrediction();

        cachedAutoEvoPredictionSpecies ??= new MicrobeSpecies(Editor.EditedBaseSpecies,
            Editor.EditedCellProperties ??
            throw new InvalidOperationException("can't start auto-evo prediction without current cell properties"),
            hexTemporaryMemory, hexTemporaryMemory2);

        // Need to copy player species property to have auto-evo treat the predicted population the same way as
        // the player in a real run
        if (Editor.EditedBaseSpecies.PlayerSpecies)
        {
            cachedAutoEvoPredictionSpecies.BecomePlayerSpecies();
        }

        CopyEditedPropertiesToSpecies(cachedAutoEvoPredictionSpecies);

        var run = new EditorAutoEvoRun(Editor.CurrentGame.GameWorld, Editor.CurrentGame.GameWorld.AutoEvoGlobalCache,
            Editor.EditedBaseSpecies, cachedAutoEvoPredictionSpecies, Editor.TargetPatch);
        run.Start();
        autoEvoPredictionDirty = false;

        UpdateAutoEvoPrediction(run, Editor.EditedBaseSpecies, cachedAutoEvoPredictionSpecies);
    }

    private void OnEnergyBalanceOptionsChanged()
    {
        CalculateEnergyAndCompoundBalance(editedMicrobeOrganelles, Membrane);

        UpdateFinishButtonWarningVisibility();
    }

    private void OnResourceLimitingModeChanged()
    {
        CalculateEnergyAndCompoundBalance(editedMicrobeOrganelles, Membrane);

        UpdateFinishButtonWarningVisibility();
    }

    private ResolvedMicrobeTolerances CalculateLatestTolerances()
    {
        if (IsMulticellularEditor)
        {
            if (!multicellularTolerancesPrinted)
            {
                GD.Print("TODO: implement tolerances data coming from the multicellular editor");
                multicellularTolerancesPrinted = true;
            }

            // TODO: this should use info from the cell body plan editor regarding tolerances and remove this dummy
            // return
            return new ResolvedMicrobeTolerances
            {
                HealthModifier = 1,
                OsmoregulationModifier = 1,
                ProcessSpeedModifier = 1,
            };
        }

        return MicrobeEnvironmentalToleranceCalculations.ResolveToleranceValues(CalculateRawTolerances());
    }

    private ToleranceResult CalculateRawTolerances()
    {
        return MicrobeEnvironmentalToleranceCalculations.CalculateTolerances(tolerancesEditor.CurrentTolerances,
            editedMicrobeOrganelles, Editor.CurrentPatch.Biome);
    }

    /// <summary>
    ///   Calculates the energy balance and compound balance for a cell with the given organelles and membrane
    /// </summary>
    private void CalculateEnergyAndCompoundBalance(IReadOnlyList<OrganelleTemplate> organelles,
        MembraneType membrane, BiomeConditions? biome = null)
    {
        biome ??= Editor.CurrentPatch.Biome;

        bool moving = organismStatisticsPanel.CalculateBalancesWhenMoving;

        IBiomeConditions conditionsData = biome;

        if (organismStatisticsPanel.ResourceLimitingMode != ResourceLimitingMode.AllResources)
        {
            conditionsData = new BiomeResourceLimiterAdapter(organismStatisticsPanel.ResourceLimitingMode,
                conditionsData);
        }

        var energyBalance = new EnergyBalanceInfoFull();
        energyBalance.SetupTrackingForRequiredCompounds();

        var maximumMovementDirection = MicrobeInternalCalculations.MaximumSpeedDirection(organelles);

        ProcessSystem.ComputeEnergyBalanceFull(organelles, conditionsData, CalculateLatestTolerances(), membrane,
            maximumMovementDirection, moving, true, Editor.CurrentGame.GameWorld.WorldSettings,
            organismStatisticsPanel.CompoundAmountType, null, energyBalance);

        energyBalanceInfo = energyBalance;

        organismStatisticsPanel.UpdateEnergyBalance(energyBalance);

        if (Visible)
        {
            TutorialState?.SendEvent(TutorialEventType.MicrobeEditorPlayerEnergyBalanceChanged,
                new EnergyBalanceEventArgs(energyBalance), this);
        }

        float nominalStorage = 0;
        Dictionary<Compound, float>? specificStorages = null;

        // This takes balanceType into account as well, https://github.com/Revolutionary-Games/Thrive/issues/2068
        var compoundBalanceData =
            CalculateCompoundBalanceWithMethod(organismStatisticsPanel.BalanceDisplayType,
                organismStatisticsPanel.CompoundAmountType, organelles, conditionsData, energyBalance,
                ref specificStorages, ref nominalStorage);

        UpdateCompoundBalances(compoundBalanceData);

        // TODO: should this skip on being affected by the resource limited?
        var nightBalanceData = CalculateCompoundBalanceWithMethod(organismStatisticsPanel.BalanceDisplayType,
            CompoundAmountType.Minimum, organelles, conditionsData, energyBalance, ref specificStorages,
            ref nominalStorage);

        UpdateCompoundLastingTimes(compoundBalanceData, nightBalanceData, nominalStorage,
            specificStorages ?? throw new Exception("Special storages should have been calculated"));

        HandleProcessList(energyBalance, conditionsData);
    }

    private Dictionary<Compound, CompoundBalance> CalculateCompoundBalanceWithMethod(BalanceDisplayType calculationType,
        CompoundAmountType amountType, IReadOnlyList<OrganelleTemplate> organelles, IBiomeConditions biome,
        EnergyBalanceInfoFull energyBalance, ref Dictionary<Compound, float>? specificStorages,
        ref float nominalStorage)
    {
        var compoundBalanceData = new Dictionary<Compound, CompoundBalance>();
        switch (calculationType)
        {
            case BalanceDisplayType.MaxSpeed:
                ProcessSystem.ComputeCompoundBalance(organelles, biome, CalculateLatestTolerances(), amountType, true,
                    compoundBalanceData);
                break;
            case BalanceDisplayType.EnergyEquilibrium:
                ProcessSystem.ComputeCompoundBalanceAtEquilibrium(organelles, biome, CalculateLatestTolerances(),
                    amountType, energyBalance, compoundBalanceData);
                break;
            default:
                GD.PrintErr("Unknown compound balance type: ", calculationType);
                goto case BalanceDisplayType.EnergyEquilibrium;
        }

        specificStorages ??= MicrobeInternalCalculations.GetTotalSpecificCapacity(organelles, out nominalStorage);

        return ProcessSystem.ComputeCompoundFillTimes(compoundBalanceData, nominalStorage, specificStorages);
    }

    private void HandleProcessList(EnergyBalanceInfoFull energyBalance, IBiomeConditions biome)
    {
        var processes = new List<TweakedProcess>();

        // Empty list to later fill
        var processStatistics = new List<ProcessSpeedInformation>();

        ProcessSystem.ComputeActiveProcessList(editedMicrobeOrganelles, ref processes);

        var tolerances = CalculateLatestTolerances();

        float consumptionProductionRatio = energyBalance.TotalConsumption / energyBalance.TotalProduction;

        foreach (var process in processes)
        {
            // This requires the inputs to be in the biome to give a realistic prediction of how fast the processes
            // *might* run once swimming around in the stage.
            var singleProcess = ProcessSystem.CalculateProcessMaximumSpeed(process, tolerances.ProcessSpeedModifier,
                biome, CompoundAmountType.Current, true);

            // If produces more ATP than consumes, lower down production for inputs and for outputs,
            // otherwise use maximum production values (this matches the equilibrium display mode and what happens
            // in the game once exiting the editor)
            if (consumptionProductionRatio < 1.0f)
            {
                singleProcess.ScaleSpeed(consumptionProductionRatio, processSpeedWorkMemory);
            }

            processStatistics.Add(singleProcess);
        }

        organismStatisticsPanel.UpdateProcessList(processStatistics);
    }

    /// <summary>
    ///   If not hovering over an organelle, render the to-be-placed organelle
    /// </summary>
    private void RenderHighlightedOrganelle(int q, int r, int rotation, OrganelleDefinition shownOrganelleDefinition,
        OrganelleUpgrades? upgrades)
    {
        RenderHoveredHex(q, r, shownOrganelleDefinition.GetRotatedHexes(rotation), isPlacementProbablyValid,
            out bool hadDuplicate);

        bool showModel = !hadDuplicate;

        // Model
        if (showModel && shownOrganelleDefinition.TryGetGraphicsScene(upgrades, out var modelInfo))
        {
            var cartesianPosition = Hex.AxialToCartesian(new Hex(q, r));

            var organelleModel = hoverModels[usedHoverModel++];

            organelleModel.Transform = new Transform3D(new Basis(MathUtils.CreateRotationForOrganelle(rotation)),
                cartesianPosition + shownOrganelleDefinition.ModelOffset);

            organelleModel.Scale = shownOrganelleDefinition.GetUpgradesSizeModification(upgrades);

            organelleModel.Visible = true;

            UpdateOrganellePlaceHolderScene(organelleModel, modelInfo, Hex.GetRenderPriority(new Hex(q, r)));
        }
    }

    /// <summary>
    ///   Places an organelle of the specified type under the cursor and also applies symmetry to
    ///   place multiple at once.
    /// </summary>
    /// <returns>True when at least one organelle got placed</returns>
    private bool AddOrganelle(string organelleType)
    {
        GetMouseHex(out int q, out int r);

        var placementActions = new List<EditorAction>();

        // For multi hex organelles we keep track of positions that got filled in
        var usedHexes = new HashSet<Hex>();

        HexEditorSymmetry? overrideSymmetry =
            componentBottomLeftButtons.SymmetryEnabled ? null : HexEditorSymmetry.None;

        RunWithSymmetry(q, r,
            (attemptQ, attemptR, rotation) =>
            {
                var organelle = new OrganelleTemplate(GetOrganelleDefinition(organelleType),
                    new Hex(attemptQ, attemptR), rotation);

                var hexes = organelle.RotatedHexes.Select(h => h + new Hex(attemptQ, attemptR)).ToList();

                foreach (var hex in hexes)
                {
                    if (usedHexes.Contains(hex))
                    {
                        // Duplicate with already placed
                        return;
                    }
                }

                var placed = CreatePlaceActionIfPossible(organelle);

                if (placed != null)
                {
                    placementActions.Add(placed);

                    foreach (var hex in hexes)
                    {
                        usedHexes.Add(hex);
                    }
                }
            }, overrideSymmetry);

        if (placementActions.Count < 1)
            return false;

        var multiAction = new CombinedEditorAction(placementActions);

        return EnqueueAction(multiAction);
    }

    /// <summary>
    ///   Helper for AddOrganelle
    /// </summary>
    private CombinedEditorAction? CreatePlaceActionIfPossible(OrganelleTemplate organelle)
    {
        if (MicrobePreviewMode)
            return null;

        if (!IsValidPlacement(organelle, true))
        {
            // Play Sound
            Editor.OnInvalidAction();
            return null;
        }

        return CreateAddOrganelleAction(organelle);
    }

    private bool IsValidPlacement(OrganelleTemplate organelle, bool allowOverwritingCytoplasm)
    {
        bool notPlacingCytoplasm = organelle.Definition.InternalName != cytoplasm.InternalName;

        if (!allowOverwritingCytoplasm)
            notPlacingCytoplasm = false;

        return editedMicrobeOrganelles.CanPlaceAndIsTouching(organelle,
            notPlacingCytoplasm, hexTemporaryMemory, hexTemporaryMemory2,
            notPlacingCytoplasm);
    }

    private CombinedEditorAction? CreateAddOrganelleAction(OrganelleTemplate organelle)
    {
        // 1 - you put a unique organelle (means only one instance allowed) but you already have it
        // 2 - you put an organelle that requires nucleus but you don't have one
        if ((organelle.Definition.Unique && HasOrganelle(organelle.Definition)) ||
            (organelle.Definition.RequiresNucleus && !HasNucleus))
        {
            return null;
        }

        if (organelle.Definition.Unique)
            DeselectOrganelleToPlace();

        var replacedCytoplasmActions =
            GetReplacedCytoplasmRemoveAction(new[] { organelle }).Cast<EditorAction>().ToList();

        var action = new SingleEditorAction<OrganellePlacementActionData>(DoOrganellePlaceAction,
            UndoOrganellePlaceAction,
            new OrganellePlacementActionData(organelle, organelle.Position, organelle.Orientation)
            {
                CostMultiplier = CostMultiplier,
            });

        replacedCytoplasmActions.Add(action);
        return new CombinedEditorAction(replacedCytoplasmActions);
    }

    /// <summary>
    ///   Finishes an organelle move
    /// </summary>
    /// <returns>True if the organelle move succeeded.</returns>
    private bool MoveOrganelle(OrganelleTemplate organelle, Hex newLocation, int newRotation)
    {
        // TODO: consider allowing rotation inplace (https://github.com/Revolutionary-Games/Thrive/issues/2993)

        if (MicrobePreviewMode)
            return false;

        // Make sure placement is valid
        if (!IsMoveTargetValid(newLocation, newRotation, organelle))
            return false;

        var multiAction = GetMultiActionWithOccupancies(
            new List<(Hex Hex, int Orientation)> { (newLocation, newRotation) },
            new List<OrganelleTemplate> { organelle }, true);

        // Too low mutation points, cancel move
        if (Editor.MutationPoints < Editor.WhatWouldActionsCost(multiAction.Data))
        {
            CancelCurrentAction();
            Editor.OnInsufficientMP(false);
            return false;
        }

        EnqueueAction(multiAction);

        // It's assumed that the above enqueue can't fail, otherwise the reference to MovingPlacedHex may be
        // permanently lost (as the code that calls this assumes it's safe to set MovingPlacedHex to null
        // when we return true)
        return true;
    }

    private bool IsNegativeAtpProduction()
    {
        return energyBalanceInfo != null &&
            energyBalanceInfo.TotalProduction < energyBalanceInfo.TotalConsumptionStationary;
    }

    private void OnPostNewMicrobeChange()
    {
        UpdateMembraneButtons(Membrane.InternalName);
        UpdateStats();
        OnRigidityChanged();
        OnColourChanged();

        StartAutoEvoPrediction();
        suggestionDirty = true;
    }

    private void OnRigidityChanged()
    {
        UpdateRigiditySlider((int)Math.Round(Rigidity * Constants.MEMBRANE_RIGIDITY_SLIDER_TO_VALUE_RATIO));

        organismStatisticsPanel.UpdateSpeed(CalculateSpeed());
        organismStatisticsPanel.UpdateRotationSpeed(CalculateRotationSpeed());
        organismStatisticsPanel.UpdateHitpoints(CalculateHitpoints());

        // Osmoregulation efficiency may play a role in the suggestion, so queue a new one
        suggestionDirty = true;
        autoEvoPredictionDirty = true;
    }

    private void OnColourChanged()
    {
        membraneColorPicker.SetColour(Colour);
    }

    private void UpdateStats()
    {
        organismStatisticsPanel.UpdateSpeed(CalculateSpeed());
        organismStatisticsPanel.UpdateRotationSpeed(CalculateRotationSpeed());
        organismStatisticsPanel.UpdateHitpoints(CalculateHitpoints());
        organismStatisticsPanel.UpdateStorage(GetAdditionalCapacities(out var nominalCapacity), nominalCapacity);
        organismStatisticsPanel.UpdateTotalDigestionSpeed(CalculateTotalDigestionSpeed());
        organismStatisticsPanel.UpdateDigestionEfficiencies(CalculateDigestionEfficiencies());
        var (ammoniaCost, phosphatesCost) = CalculateOrganellesCosts();
        organismStatisticsPanel.UpdateOrganellesCost(ammoniaCost, phosphatesCost);
    }

    /// <summary>
    ///   Lock / unlock the organelles that need a nucleus
    /// </summary>
    private void UpdatePartsAvailability(List<OrganelleDefinition> placedUniqueOrganelleNames)
    {
        foreach (var organelle in placeablePartSelectionElements.Keys)
        {
            UpdatePartAvailability(placedUniqueOrganelleNames, organelle);
        }
    }

    private void OnOrganelleToPlaceSelected(string organelle)
    {
        if (ActiveActionName == organelle)
            return;

        ActiveActionName = organelle;

        ApplySymmetryForCurrentOrganelle();
        UpdateOrganelleButtons(organelle);
    }

    private void DeselectOrganelleToPlace()
    {
        ActiveActionName = null;
        UpdateOrganelleButtons(null);
    }

    private void UpdateOrganelleButtons(string? selectedOrganelle)
    {
        // Update the icon highlightings
        foreach (var selection in placeablePartSelectionElements.Values)
        {
            selection.Selected = selection.Name.ToString() == selectedOrganelle;
        }
    }

    private void UpdateMembraneButtons(string membrane)
    {
        // Update the icon highlightings
        foreach (var selection in membraneSelectionElements.Values)
        {
            selection.Selected = selection.Name.ToString() == membrane;
        }
    }

    private void OnOrganellesChanged()
    {
        UpdateAlreadyPlacedVisuals();

        UpdateArrow();

        UpdatePartsAvailability(PlacedUniqueOrganelles.ToList());

        UpdatePatchDependentBalanceData();

        // Send to gui current status of cell
        organismStatisticsPanel.UpdateSize(MicrobeHexSize);
        UpdateStats();

        if (!IsMulticellularEditor)
        {
            // Tolerances are now affected by organelle changes, so re-trigger calculating them
            OnTolerancesChanged(tolerancesEditor.CurrentTolerances);
            tolerancesEditor.OnDataTolerancesDependOnChanged();
        }

        UpdateCellVisualization();

        StartAutoEvoPrediction();
        suggestionDirty = true;

        UpdateFinishButtonWarningVisibility();

        // Updated here to make sure everything else has been updated first so tooltips are accurate
        UpdateOrganelleUnlockTooltips(false);

        UpdateGrowthOrderButtons();
    }

    /// <summary>
    ///   This destroys and creates again entities to represent all the currently placed organelles. Call this whenever
    ///   editedMicrobeOrganelles is changed.
    /// </summary>
    private void UpdateAlreadyPlacedVisuals()
    {
        editedMicrobeOrganelles.GetIslandHexes(islandResults, islandsWorkMemory1, islandsWorkMemory2,
            islandsWorkMemory3);

        // TODO: The code below is partly duplicate to CellHexPhotoBuilder. If this is changed that needs changes too.
        // Build the entities to show the current microbe
        UpdateAlreadyPlacedHexes(editedMicrobeOrganelles.Select(o => (o.Position, o.RotatedHexes,
            Editor.HexPlacedThisSession<OrganelleTemplate, CellType>(o))), islandResults, microbePreviewMode);

        int nextFreeOrganelle = 0;

        foreach (var organelle in editedMicrobeOrganelles)
        {
            // Hexes are handled by UpdateAlreadyPlacedHexes

            // Model of the organelle
            if (organelle.Definition.TryGetGraphicsScene(organelle.Upgrades, out var modelInfo))
            {
                if (nextFreeOrganelle >= placedModels.Count)
                {
                    // New organelle model needed
                    placedModels.Add(CreatePreviewModelHolder());
                }

                var organelleModel = placedModels[nextFreeOrganelle++];

                UpdateOrganelleDisplayerTransform(organelleModel, organelle);

                organelleModel.Visible = !MicrobePreviewMode;

                UpdateOrganellePlaceHolderScene(organelleModel, modelInfo, Hex.GetRenderPriority(organelle.Position));
            }
        }

        while (nextFreeOrganelle < placedModels.Count)
        {
            placedModels[placedModels.Count - 1].DetachAndQueueFree();
            placedModels.RemoveAt(placedModels.Count - 1);
        }
    }

    private void SetSpeciesInfo(string name, MembraneType membrane, Color colour, float rigidity,
        BehaviourDictionary? behaviour)
    {
        componentBottomLeftButtons.SetNewName(name);

        membraneColorPicker.Color = colour;

        UpdateMembraneButtons(membrane.InternalName);
        SetMembraneTooltips(membrane);

        UpdateRigiditySlider((int)Math.Round(rigidity * Constants.MEMBRANE_RIGIDITY_SLIDER_TO_VALUE_RATIO));

        // TODO: put this call in some better place (also in CellBodyPlanEditorComponent)
        if (!IsMulticellularEditor)
        {
            behaviourEditor.UpdateAllBehaviouralSliders(behaviour ??
                throw new ArgumentNullException(nameof(behaviour)));
        }

        // Tolerances are applied directly in the OnEditorSpeciesSetup method
    }

    private void OnMovePressed()
    {
        if (Settings.Instance.MoveOrganellesWithSymmetry.Value)
        {
            // Start moving the organelles symmetrically to the clicked organelle.
            StartHexMoveWithSymmetry(organelleMenu.GetSelectedThatAreStillValid(editedMicrobeOrganelles));
        }
        else
        {
            StartHexMove(organelleMenu.GetSelectedThatAreStillValid(editedMicrobeOrganelles).FirstOrDefault());
        }
    }

    private void OnDeletePressed()
    {
        int alreadyDeleted = 0;
        var targets = organelleMenu.GetSelectedThatAreStillValid(editedMicrobeOrganelles)
            .Select(o => TryCreateRemoveHexAtAction(o.Position, ref alreadyDeleted)).WhereNotNull().ToList();

        if (targets.Count < 1)
        {
            GD.PrintErr("No targets found to delete");
            return;
        }

        var action = new CombinedEditorAction(targets);

        EnqueueAction(action);
    }

    private void OnModifyPressed()
    {
        var targetOrganelle = organelleMenu.GetSelectedThatAreStillValid(editedMicrobeOrganelles).FirstOrDefault();

        if (targetOrganelle == null)
        {
            GD.PrintErr("Target to modify has disappeared");
            return;
        }

        var upgradeGUI = targetOrganelle.Definition.UpgradeGUI;

        if (!IsUpgradingPossibleFor(targetOrganelle.Definition))
        {
            GD.PrintErr("Attempted to modify an organelle that can't be upgraded");
            return;
        }

        if (TutorialState?.Enabled == true)
        {
            TutorialState.SendEvent(TutorialEventType.MicrobeEditorOrganelleModified, EventArgs.Empty, this);
        }

        organelleUpgradeGUI.OpenForOrganelle(targetOrganelle, upgradeGUI ?? string.Empty, this, Editor, CostMultiplier,
            Editor.CurrentGame);
    }

    /// <summary>
    ///   Lock / unlock organelle buttons that need a nucleus or are already placed (if unique)
    /// </summary>
    private void UpdatePartAvailability(List<OrganelleDefinition> placedUniqueOrganelleNames,
        OrganelleDefinition organelle)
    {
        var item = placeablePartSelectionElements[organelle];

        if (organelle.Unique && placedUniqueOrganelleNames.Contains(organelle))
        {
            item.Locked = true;
        }
        else if (organelle.RequiresNucleus && !placedUniqueOrganelleNames.Contains(nucleus))
        {
            item.Locked = true;
        }
        else
        {
            item.Locked = false;
        }

        item.RecentlyUnlocked = Editor.CurrentGame.GameWorld.UnlockProgress.RecentlyUnlocked(organelle);
    }

    /// <summary>
    ///   Creates part and membrane selection buttons
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     This doesn't multiply the shown MP Cost by the cost factor as this is called much earlier before editor is
    ///     initialized proper, for that use <see cref="UpdateMicrobePartSelections"/> or <see cref="UpdateMPCost"/>.
    ///   </para>
    /// </remarks>
    private void SetupMicrobePartSelections()
    {
        var simulationParameters = SimulationParameters.Instance;

        var organelleButtonGroup = new ButtonGroup();
        var membraneButtonGroup = new ButtonGroup();

        foreach (var organelle in simulationParameters.GetAllOrganelles().OrderBy(o => o.EditorButtonOrder))
        {
            if (organelle.EditorButtonGroup == OrganelleDefinition.OrganelleGroup.Hidden)
                continue;

            var group = partsSelectionContainer.GetNode<CollapsibleList>(organelle.EditorButtonGroup.ToString());

            if (group == null)
            {
                GD.PrintErr("No node found for organelle selection button for ", organelle.InternalName);
                return;
            }

            var control = organelleSelectionButtonScene.Instantiate<MicrobePartSelection>();
            control.Locked = organelle.Unimplemented;
            control.PartIcon = organelle.LoadedIcon ?? throw new Exception("Organelle with no icon");
            control.PartName = organelle.UntranslatedName;
            control.SelectionGroup = organelleButtonGroup;
            control.MPCost = organelle.MPCost;
            control.Name = organelle.InternalName;

            // Special case with registering the tooltip here for item with no associated organelle
            control.RegisterToolTipForControl(organelle.InternalName, "organelleSelection");

            group.AddItem(control);

            allPartSelectionElements.Add(organelle, control);

            if (organelle.Unimplemented)
                continue;

            // Only add items with valid organelles to dictionary
            placeablePartSelectionElements.Add(organelle, control);

            control.Connect(MicrobePartSelection.SignalName.OnPartSelected,
                new Callable(this, nameof(OnOrganelleToPlaceSelected)));
        }

        foreach (var membraneType in simulationParameters.GetAllMembranes().OrderBy(m => m.EditorButtonOrder))
        {
            var control = organelleSelectionButtonScene.Instantiate<MicrobePartSelection>();
            control.PartIcon = membraneType.LoadedIcon;
            control.PartName = membraneType.UntranslatedName;
            control.SelectionGroup = membraneButtonGroup;
            control.MPCost = membraneType.EditorCost;
            control.Name = membraneType.InternalName;

            control.RegisterToolTipForControl(membraneType.InternalName, "membraneSelection");

            membraneTypeSelection.AddItem(control);

            membraneSelectionElements.Add(membraneType, control);

            control.Connect(MicrobePartSelection.SignalName.OnPartSelected,
                new Callable(this, nameof(OnMembraneSelected)));
        }

        // Multicellular parts only available (visible) in multicellular
        // For now there aren't any multicellular specific organelles so the section is hidden
        partsSelectionContainer.GetNode<CollapsibleList>(OrganelleDefinition.OrganelleGroup.Multicellular.ToString())
            .Visible = false;

        // TODO: put this code back in if we get multicellular specific organelles
        // .Visible = IsMulticellularEditor;

        partsSelectionContainer.GetNode<CollapsibleList>(OrganelleDefinition.OrganelleGroup.Macroscopic.ToString())
            .Visible = IsMacroscopicEditor;
    }

    private bool HasNewName()
    {
        if (Editor.EditedCellProperties == null)
        {
            return false;
        }

        return Editor.EditedCellProperties.FormattedName != newName;
    }

    private void OnSpeciesNameChanged(string newText)
    {
        newName = newText;

        if (IsMulticellularEditor)
        {
            if (HasNewName())
            {
                componentBottomLeftButtons.ReportValidityOfName(ValidateNewCellTypeName!(newText));
            }
            else
            {
                // The name hasn't changed and should remain valid
                componentBottomLeftButtons.ReportValidityOfName(true);
            }
        }
    }

    private void OnColorChanged(Color color)
    {
        if (MovingPlacedHex != null)
        {
            Editor.OnActionBlockedWhileMoving();
            membraneColorPicker.SetColour(Colour);
            return;
        }

        if (Colour == color)
            return;

        var action = new SingleEditorAction<ColourActionData>(DoColourChangeAction, UndoColourChangeAction,
            new ColourActionData(color, Colour)
            {
                CostMultiplier = CostMultiplier,
            });

        Editor.EnqueueAction(action);
    }

    /// <summary>
    ///   "Searches" an organelle selection button by hiding the ones whose name doesn't include the input substring
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     NOTE: this is not currently used
    ///   </para>
    /// </remarks>
    private void OnSearchBoxTextChanged(string newText)
    {
        var input = newText.ToLower(CultureInfo.InvariantCulture);

        var organelles = SimulationParameters.Instance.GetAllOrganelles()
            .Where(o => o.Name.ToLower(CultureInfo.CurrentCulture).Contains(input)).ToList();

        foreach (var node in placeablePartSelectionElements.Values)
        {
            // To show back organelles that simulation parameters didn't include
            if (string.IsNullOrEmpty(input))
            {
                node.Show();
                continue;
            }

            node.Hide();

            foreach (var organelle in organelles)
            {
                if (node.Name == organelle.InternalName)
                {
                    node.Show();
                }
            }
        }
    }

    /// <summary>
    ///   Copies current editor state to a species
    /// </summary>
    /// <param name="target">The species to copy to</param>
    /// <remarks>
    ///   <para>
    ///     TODO: it would be nice to unify this and the final apply properties to the edited species. There's also
    ///     almost a duplicate in OrganelleSuggestionCalculation
    ///   </para>
    /// </remarks>
    private void CopyEditedPropertiesToSpecies(MicrobeSpecies target)
    {
        target.Colour = Colour;
        target.MembraneType = Membrane;
        target.MembraneRigidity = Rigidity;
        target.IsBacteria = true;

        target.Organelles.Clear();

        // TODO: if this is too slow to copy each organelle like this, we'll need to find a faster way to get the data
        // in, perhaps by sharing the entire Organelles object
        foreach (var entry in editedMicrobeOrganelles.Organelles)
        {
            if (entry.Definition == nucleus)
                target.IsBacteria = false;

            target.Organelles.AddFast(entry, hexTemporaryMemory, hexTemporaryMemory2);
        }

        // Copy behaviour if it is known
        if (overwriteBehaviourForCalculations != null)
        {
            // Make a clone to make sure data cannot change while running
            target.Behaviour = overwriteBehaviourForCalculations.CloneObject();
        }

        // Copy tolerances
        target.Tolerances.CopyFrom(tolerancesEditor.CurrentTolerances);
    }

    private void SetLightLevelOption(int option)
    {
        // Show selected light level
        switch ((LightLevelOption)option)
        {
            case LightLevelOption.Day:
            {
                Editor.DayLightFraction = 1;
                break;
            }

            case LightLevelOption.Night:
            {
                Editor.DayLightFraction = 0;
                break;
            }

            case LightLevelOption.Average:
            {
                Editor.DayLightFraction = Editor.CurrentGame.GameWorld.LightCycle.AverageSunlight;
                break;
            }

            case LightLevelOption.Current:
            {
                Editor.DayLightFraction = Editor.CurrentGame.GameWorld.LightCycle.DayLightFraction;
                break;
            }

            default:
                throw new Exception("Invalid light level option");
        }
    }

    private void SetSelectionMenuTab(string tab)
    {
        var selection = (SelectionMenuTab)Enum.Parse(typeof(SelectionMenuTab), tab);

        if (selection == selectedSelectionMenuTab)
            return;

        GUICommon.Instance.PlayButtonPressSound();

        if (!BlockTabSwitchIfInProgressAction(CanCancelAction))
        {
            selectedSelectionMenuTab = selection;
            tutorialState?.SendEvent(TutorialEventType.CellEditorTabChanged, new StringEventArgs(tab), this);
        }

        ApplySelectionMenuTab();
    }

    private void ApplySelectionMenuTab()
    {
        // Hide all
        structureTab.Hide();
        appearanceTab.Hide();
        behaviourEditor.Hide();
        growthOrderTab.Hide();
        toleranceTab.Hide();

        // Show selected
        switch (selectedSelectionMenuTab)
        {
            case SelectionMenuTab.Structure:
            {
                structureTab.Show();
                structureTabButton.ButtonPressed = true;
                MicrobePreviewMode = false;
                ShowGrowthOrder = false;
                break;
            }

            case SelectionMenuTab.Membrane:
            {
                appearanceTab.Show();
                appearanceTabButton.ButtonPressed = true;
                MicrobePreviewMode = true;
                ShowGrowthOrder = false;
                break;
            }

            case SelectionMenuTab.Behaviour:
            {
                behaviourEditor.Show();
                behaviourTabButton.ButtonPressed = true;
                MicrobePreviewMode = false;
                ShowGrowthOrder = false;
                break;
            }

            case SelectionMenuTab.GrowthOrder:
            {
                growthOrderTab.Show();
                growthOrderTabButton.ButtonPressed = true;
                MicrobePreviewMode = false;
                ShowGrowthOrder = true;

                UpdateGrowthOrderButtons();
                break;
            }

            case SelectionMenuTab.Tolerance:
            {
                toleranceTab.Show();
                toleranceTabButton.ButtonPressed = true;
                MicrobePreviewMode = false;
                ShowGrowthOrder = false;
                break;
            }

            default:
                throw new Exception("Invalid selection menu tab");
        }
    }

    private void UpdateAutoEvoPredictionTranslations()
    {
        if (autoEvoPredictionRunSuccessful == false)
        {
            totalEnergyLabel.Value = float.NaN;
            autoEvoPredictionFailedLabel.Show();
        }
        else
        {
            autoEvoPredictionFailedLabel.Hide();
        }

        var energyFormat = Localization.Translate("ENERGY_IN_PATCH_SHORT");

        if (!string.IsNullOrEmpty(bestPatchName))
        {
            var formatted = StringUtils.ThreeDigitFormat(bestPatchEnergyGathered);

            bestPatchLabel.Text =
                energyFormat.FormatSafe(Localization.Translate(bestPatchName), formatted);
        }
        else
        {
            bestPatchLabel.Text = Localization.Translate("N_A");
        }

        if (!string.IsNullOrEmpty(worstPatchName))
        {
            var formatted = StringUtils.ThreeDigitFormat(worstPatchEnergyGathered);

            worstPatchLabel.Text =
                energyFormat.FormatSafe(Localization.Translate(worstPatchName), formatted);
        }
        else
        {
            worstPatchLabel.Text = Localization.Translate("N_A");
        }
    }

    private void DummyKeepTranslation()
    {
        // This keeps this translation string existing if we ever still want to use worst and best population numbers
        Localization.Translate("POPULATION_IN_PATCH_SHORT");
    }

    private void OpenAutoEvoPredictionDetails()
    {
        GUICommon.Instance.PlayButtonPressSound();

        UpdateAutoEvoPredictionDetailsText();

        autoEvoPredictionExplanationPopup.PopupCenteredShrink();

        TutorialState?.SendEvent(TutorialEventType.MicrobeEditorAutoEvoPredictionOpened, EventArgs.Empty, this);
    }

    private void CloseAutoEvoPrediction()
    {
        GUICommon.Instance.PlayButtonPressSound();
        autoEvoPredictionExplanationPopup.Hide();
    }

    private void OnAutoEvoPredictionComplete(PendingAutoEvoPrediction run)
    {
        if (!run.AutoEvoRun.WasSuccessful)
        {
            GD.PrintErr("Failed to run auto-evo prediction for showing in the editor");
            autoEvoPredictionRunSuccessful = false;
            UpdateAutoEvoPredictionTranslations();
            return;
        }

        var results = run.AutoEvoRun.Results ??
            throw new Exception("Auto evo prediction has no results even though it succeeded");

        // Total population
        var newPopulation = results.GetGlobalPopulation(run.PlayerSpeciesNew);

        autoEvoPredictionRunSuccessful = true;

        // Gather energy details
        float totalEnergy = 0;
        Patch? bestPatch = null;
        float bestPatchEnergy = 0;
        Patch? worstPatch = null;
        float worstPatchEnergy = 0;

        foreach (var entry in results.GetPatchEnergyResults(run.PlayerSpeciesNew))
        {
            // Best
            if (bestPatch == null || bestPatchEnergy < entry.Value.TotalEnergyGathered)
            {
                bestPatchEnergy = entry.Value.TotalEnergyGathered;
                bestPatch = entry.Key;
            }

            if (worstPatch == null || worstPatchEnergy > entry.Value.TotalEnergyGathered)
            {
                worstPatchEnergy = entry.Value.TotalEnergyGathered;
                worstPatch = entry.Key;
            }

            totalEnergy += entry.Value.TotalEnergyGathered;
        }

        // Set the initial value to compare against the original species
        totalEnergyLabel.ResetInitialValue();

        if (PreviousPlayerGatheredEnergy != null)
        {
            totalEnergyLabel.Value = PreviousPlayerGatheredEnergy.Value;
            totalEnergyLabel.TooltipText =
                new LocalizedString("GATHERED_ENERGY_TOOLTIP", PreviousPlayerGatheredEnergy).ToString();
        }
        else
        {
            GD.PrintErr("Previously gathered energy is unknown, can't compare them (this will happen with " +
                "older saves)");
        }

        var formatted = StringUtils.ThreeDigitFormat(totalEnergy);

        totalEnergyLabel.SetMultipartValue($"{formatted} ({newPopulation} {Constants.MICROBE_POPULATION_SUFFIX})",
            totalEnergy);

        // Set the best and worst patch displays
        worstPatchName = worstPatch?.Name.ToString();
        worstPatchEnergyGathered = worstPatchEnergy;

        if (worstPatch != null)
        {
            // For some reason in rare cases the population numbers cannot be found, using FirstOrDefault should ensure
            // here that missing population numbers get assigned 0
            worstPatchPopulation = results.GetPopulationInPatches(run.PlayerSpeciesNew)
                .FirstOrDefault(p => p.Key == worstPatch).Value;
        }

        bestPatchName = bestPatch?.Name.ToString();
        bestPatchEnergyGathered = bestPatchEnergy;

        if (bestPatch != null)
        {
            bestPatchPopulation = results.GetPopulationInPatches(run.PlayerSpeciesNew)
                .FirstOrDefault(p => p.Key == bestPatch).Value;
        }

        CreateAutoEvoPredictionDetailsText(results.GetPatchEnergyResults(run.PlayerSpeciesNew),
            run.PlayerSpeciesOriginal.FormattedNameBbCode);

        UpdateAutoEvoPredictionTranslations();

        if (autoEvoPredictionPanel.Visible)
        {
            UpdateAutoEvoPredictionDetailsText();
        }

        predictionMiches = results.GetMicheForPatch(Editor.CurrentPatch);
    }

    private void CreateAutoEvoPredictionDetailsText(
        Dictionary<Patch, RunResults.SpeciesPatchEnergyResults> energyResults, string playerSpeciesName)
    {
        predictionDetailsText = new LocalizedStringBuilder(300);

        double Round(float value)
        {
            if (value > 0.0005f)
                return Math.Round(value, 3);

            // Small values can get tiny (and still be different from getting 0 energy due to fitness), so
            // this is here for that reason
            return Math.Round(value, 8);
        }

        // This loop shows all the patches the player species is in. Could perhaps just show the current one
        foreach (var energyResult in energyResults)
        {
            predictionDetailsText.Append(new LocalizedString("ENERGY_IN_PATCH_FOR",
                energyResult.Key.Name, playerSpeciesName));
            predictionDetailsText.Append('\n');

            predictionDetailsText.Append(new LocalizedString("ENERGY_SUMMARY_LINE",
                Round(energyResult.Value.TotalEnergyGathered), Round(energyResult.Value.IndividualCost),
                $"{energyResult.Value.UnadjustedPopulation} {Constants.MICROBE_POPULATION_SUFFIX}"));

            predictionDetailsText.Append('\n');
            predictionDetailsText.Append('\n');

            predictionDetailsText.Append(new LocalizedString("ENERGY_SOURCES"));
            predictionDetailsText.Append('\n');

            foreach (var nicheInfo in energyResult.Value.PerNicheEnergy)
            {
                var data = nicheInfo.Value;
                predictionDetailsText.Append(new LocalizedString("FOOD_SOURCE_ENERGY_INFO", nicheInfo.Key,
                    Round(data.CurrentSpeciesEnergy), Round(data.CurrentSpeciesFitness),
                    Round(data.TotalAvailableEnergy),
                    Round(data.TotalFitness)));
                predictionDetailsText.Append('\n');
            }

            predictionDetailsText.Append('\n');
        }
    }

    private void UpdateAutoEvoPredictionDetailsText()
    {
        autoEvoPredictionExplanationLabel.ExtendedBbcode = predictionDetailsText != null ?
            predictionDetailsText.ToString() :
            Localization.Translate("NO_DATA_TO_SHOW");
    }

    private OrganelleDefinition GetOrganelleDefinition(string name)
    {
        return SimulationParameters.Instance.GetOrganelleType(name);
    }

    private void ApplySymmetryForCurrentOrganelle()
    {
        if (ActiveActionName == null)
            return;

        var organelle = GetOrganelleDefinition(ActiveActionName);
        componentBottomLeftButtons.SymmetryEnabled = !organelle.Unique;
    }

    private void OnMicheViewRequested()
    {
        GUICommon.Instance.PlayButtonPressSound();

        if (predictionMiches == null)
        {
            GD.PrintErr("Missing miches data, can't show the popup");
            return;
        }

        micheViewer.ShowMiches(Editor.CurrentPatch, predictionMiches, Editor.CurrentGame.GameWorld.WorldSettings);

        autoEvoPredictionExplanationPopup.Hide();
    }

    private class PendingAutoEvoPrediction
    {
        public AutoEvoRun AutoEvoRun;
        public Species PlayerSpeciesOriginal;
        public Species PlayerSpeciesNew;

        public PendingAutoEvoPrediction(AutoEvoRun autoEvoRun, Species playerSpeciesOriginal, Species playerSpeciesNew)
        {
            AutoEvoRun = autoEvoRun;
            PlayerSpeciesOriginal = playerSpeciesOriginal;
            PlayerSpeciesNew = playerSpeciesNew;
        }

        public bool Finished => AutoEvoRun.Finished;
    }

    /// <summary>
    ///   Holds data for the organelle suggestion calculation run
    /// </summary>
    private class OrganelleSuggestionCalculation
    {
        private readonly List<OrganelleDefinition> availableOrganelles = new();
        private readonly MicrobeSpecies calculationSpecies;
        private readonly MicrobeSpecies pristineSpeciesCopy;
        private readonly Action<MicrobeSpecies> applyLatestEditsToSpecies;
        private readonly GameProperties currentGameProperties;
        private readonly Species editorOpenedForSpecies;
        private readonly SimulationCache simulationCache;

        private readonly Random random = new();
        private readonly List<Hex> workMemory1 = new();
        private readonly List<Hex> workMemory2 = new();
        private readonly HashSet<Hex> workMemory3 = new();

        private AutoEvoRun? currentRun;
        private BiomeConditions? biome;
        private Patch? patch;

        private bool calculatedNoChange;
        private double bestResult;
        private OrganelleDefinition? bestOrganelle;

        private bool resultRead;

        public OrganelleSuggestionCalculation(MicrobeSpecies initialSpeciesToCopy,
            Action<MicrobeSpecies> applyLatestEditsToSpecies, GameProperties currentGameProperties,
            Species editedSpecies)
        {
            pristineSpeciesCopy = initialSpeciesToCopy;
            calculationSpecies = initialSpeciesToCopy.Clone(true);
            this.applyLatestEditsToSpecies = applyLatestEditsToSpecies;
            this.currentGameProperties = currentGameProperties;
            editorOpenedForSpecies = editedSpecies;

            simulationCache = new SimulationCache(currentGameProperties.GameWorld.WorldSettings);
        }

        public bool IsCompleted { get; private set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        /// <summary>
        ///   If true, then pure population numbers are used as the score for the suggestions (if false an energy
        ///   estimation is used). This value can be changed to experiment with the results.
        /// </summary>
        public bool UsePurePopulationScore { get; set; }

        /// <summary>
        ///   Set up this for a new suggestion calculation
        /// </summary>
        /// <param name="organellesToTry">Valid organelles to try in the suggestion</param>
        /// <param name="selectedPatch">Patch conditions to simulate in</param>
        public void StartNew(List<OrganelleDefinition> organellesToTry, Patch selectedPatch)
        {
            biome = selectedPatch.Biome;
            patch = selectedPatch;

            if (currentRun != null)
            {
                GD.PrintErr("Starting new suggestion run even though there is one in-progress");
                currentRun.Abort();
                currentRun = null;
            }

            availableOrganelles.Clear();
            availableOrganelles.AddRange(organellesToTry);

            // Refresh the latest edits to our local pristine copy that is then used by a background thread
            applyLatestEditsToSpecies(pristineSpeciesCopy);

            calculatedNoChange = false;
            bestOrganelle = null;
            bestResult = -1;
            resultRead = false;
            IsCompleted = false;

            StartNextRun();
        }

        /// <summary>
        ///   Checks current status (and starts more simulations if still queued)
        /// </summary>
        public void CheckProgress()
        {
            // When no run is in progress need to start the next one
            if (currentRun != null && !currentRun.Finished)
                return;

            if (currentRun != null)
            {
                if (currentRun.Results == null)
                    throw new InvalidOperationException("Auto-evo suggestion doesn't have results object");

                if (biome == null)
                    throw new InvalidOperationException("Biome not initialized for suggestion");

                double score;
                if (UsePurePopulationScore)
                {
                    score = currentRun.Results.GetGlobalPopulation(calculationSpecies);
                }
                else
                {
                    // Need to clear cache as we re-use the species objects, so caching would be incorrect
                    simulationCache.Clear();

                    var individualCost =
                        MichePopulation.CalculateMicrobeIndividualCost(calculationSpecies, biome, simulationCache);

                    score = currentRun.Results.GetGlobalPopulation(calculationSpecies) * individualCost;
                }

                // Store result of run
                if (!calculatedNoChange)
                {
                    // Calculating no change energy so don't apply any changes
                    calculatedNoChange = true;

                    // This is always calculated first, so we can just directly set the result
                    // Maybe add like 1-2% extra here to ensure that very marginal improvements aren't suggested?
                    // But that's probably not needed any more with this issue closed:
                    // https://github.com/Revolutionary-Games/Thrive/issues/5799
                    bestResult = score;
                }
                else
                {
                    var last = availableOrganelles[^1];
                    availableOrganelles.RemoveAt(availableOrganelles.Count - 1);

                    if (score > bestResult)
                    {
                        bestResult = score;
                        bestOrganelle = last;
                    }
                }

                currentRun = null;
            }

            if (!StartNextRun())
            {
                // Cannot start the next run, this is complete
                IsCompleted = true;
            }
        }

        public OrganelleDefinition? GetResult()
        {
            return bestOrganelle;
        }

        public bool ReadAndResetResultFlag()
        {
            if (!resultRead && IsCompleted)
            {
                resultRead = true;
                return true;
            }

            return false;
        }

        private void CopyPristineToCalculation()
        {
            // TODO: there is duplication between this and CopyEditedPropertiesToSpecies
            calculationSpecies.Colour = pristineSpeciesCopy.Colour;
            calculationSpecies.MembraneType = pristineSpeciesCopy.MembraneType;
            calculationSpecies.MembraneRigidity = pristineSpeciesCopy.MembraneRigidity;
            calculationSpecies.IsBacteria = pristineSpeciesCopy.IsBacteria;

            // This can't be undone but should be fine as the species to edit cannot change in the editor
            if (pristineSpeciesCopy.PlayerSpecies)
                calculationSpecies.BecomePlayerSpecies();

            calculationSpecies.Organelles.Clear();

            foreach (var entry in pristineSpeciesCopy.Organelles)
            {
                calculationSpecies.Organelles.AddFast(entry, workMemory1, workMemory2);
            }

            calculationSpecies.Behaviour = pristineSpeciesCopy.Behaviour;
            calculationSpecies.Tolerances.CopyFrom(pristineSpeciesCopy.Tolerances);
        }

        private bool StartNextRun()
        {
            if (currentRun != null && !currentRun.Finished)
            {
                GD.PrintErr("Previous run should have been finished before starting next as part of suggestion");
            }

            // Set up the temporary species for the suggestion run
            CopyPristineToCalculation();

            if (!calculatedNoChange)
            {
                // Calculating no change energy so don't apply any changes
            }
            else
            {
                while (true)
                {
                    if (availableOrganelles.Count < 1)
                    {
                        // Everything is already tested
                        return false;
                    }

                    var last = availableOrganelles[^1];

                    if (!CommonMutationFunctions.AddOrganelleWithStrategy(last.SuggestionPlacement, last,
                            CommonMutationFunctions.Direction.Neutral, calculationSpecies, workMemory1, workMemory2,
                            workMemory3, random))
                    {
                        GD.PrintErr($"Cannot find placement for organelle suggestion test with {last.InternalName}");
                        availableOrganelles.RemoveAt(availableOrganelles.Count - 1);
                        continue;
                    }

                    // Succeeded in adding the organelle to the test, can continue to starting the run
                    break;
                }
            }

            currentRun = new EditorAutoEvoRun(currentGameProperties.GameWorld,
                currentGameProperties.GameWorld.AutoEvoGlobalCache, editorOpenedForSpecies, calculationSpecies, patch)
            {
                // Needed in order for the suggestion to not suggest slapping down a nucleus just to benefit from the
                // player population clamp and increase in individual cost (which would unfairly give score in
                // individual-adjusted scoring mode)
                ApplyPlayerPopulationChangeClamp = UsePurePopulationScore,
                CollectEnergyInfo = false,
            };

            currentRun.Start();

            return true;
        }
    }
}
