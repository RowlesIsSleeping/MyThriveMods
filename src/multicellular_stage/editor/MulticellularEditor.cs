﻿using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Newtonsoft.Json;

/// <summary>
///   The multicellular stage editor main class
/// </summary>
[JsonObject(IsReference = true)]
[SceneLoadedClass("res://src/multicellular_stage/editor/MulticellularEditor.tscn", UsesEarlyResolve = false)]
[DeserializedCallbackTarget]
public partial class MulticellularEditor : EditorBase<EditorAction, MicrobeStage>, IEditorReportData,
    ICellEditorData
{
#pragma warning disable CA2213
    [JsonProperty]
    [AssignOnlyChildItemsOnDeserialize]
    [Export]
    private MicrobeEditorReportComponent reportTab = null!;

    [JsonProperty]
    [AssignOnlyChildItemsOnDeserialize]
    [Export]
    private MicrobeEditorPatchMap patchMapTab = null!;

    [JsonProperty]
    [AssignOnlyChildItemsOnDeserialize]
    [Export]
    private CellBodyPlanEditorComponent bodyPlanEditorTab = null!;

    [JsonProperty]
    [AssignOnlyChildItemsOnDeserialize]
    [Export]
    private CellEditorComponent cellEditorTab = null!;

    [Export]
    private Control noCellTypeSelected = null!;
#pragma warning restore CA2213

    [JsonProperty]
    private MulticellularSpecies? editedSpecies;

    [JsonProperty]
    private CellType? selectedCellTypeToEdit;

    public override bool CanCancelAction
    {
        get
        {
            if (bodyPlanEditorTab.Visible)
                return bodyPlanEditorTab.CanCancelAction;

            if (cellEditorTab.Visible)
                return cellEditorTab.CanCancelAction;

            return false;
        }
    }

    [JsonIgnore]
    public override Species EditedBaseSpecies =>
        editedSpecies ?? throw new InvalidOperationException("species not initialized");

    [JsonIgnore]
    public MulticellularSpecies EditedSpecies =>
        editedSpecies ?? throw new InvalidOperationException("species not initialized");

    [JsonIgnore]
    public Patch CurrentPatch => patchMapTab.CurrentPatch;

    [JsonIgnore]
    public Patch? TargetPatch => patchMapTab.TargetPatch;

    [JsonIgnore]
    public Patch? SelectedPatch => patchMapTab.SelectedPatch;

    [JsonIgnore]
    public ICellDefinition? EditedCellProperties => selectedCellTypeToEdit;

    // TODO: could implement this if desired but for now this is always null (might be needed for multicellular
    // tolerances implementation)
    [JsonIgnore]
    public IReadOnlyList<OrganelleTemplate>? EditedCellOrganelles => null;

    protected override string MusicCategory => "MulticellularEditor";

    protected override MainGameState ReturnToState => MainGameState.MicrobeStage;

    protected override string EditorLoadingMessage =>
        Localization.Translate("LOADING_MULTICELLULAR_EDITOR");

    protected override bool HasInProgressAction => CanCancelAction;

    public void SendAutoEvoResultsToReportComponent()
    {
        if (autoEvoResults == null)
        {
            GD.PrintErr("Unexpectedly missing auto-evo results");
            return;
        }

        UpdateAutoEvoToReportTab();
    }

    public override void SetEditorObjectVisibility(bool shown)
    {
        base.SetEditorObjectVisibility(shown);

        bodyPlanEditorTab.SetEditorWorldGuideObjectVisibility(shown);
        cellEditorTab.SetEditorWorldGuideObjectVisibility(shown);
    }

    public void OnCurrentPatchUpdated(Patch patch)
    {
        bodyPlanEditorTab.OnCurrentPatchUpdated(patch);

        cellEditorTab.OnCurrentPatchUpdated(patch);

        cellEditorTab.UpdateBackgroundImage(patch);
    }

    public override void AddContextToActions(IEnumerable<CombinableActionData> actions)
    {
        // If a cell type is being edited, add its type to each action data, so that we can use it for undoing and
        // redoing later
        if (selectedEditorTab == EditorTab.CellTypeEditor && selectedCellTypeToEdit != null)
        {
            foreach (var actionData in actions)
            {
                if (actionData is EditorCombinableActionData<CellType> cellTypeData && cellTypeData.Context == null)
                    cellTypeData.Context = selectedCellTypeToEdit;
            }
        }
    }

    public override bool CancelCurrentAction()
    {
        if (bodyPlanEditorTab.Visible)
        {
            return bodyPlanEditorTab.CancelCurrentAction();
        }

        if (cellEditorTab.Visible)
        {
            return cellEditorTab.CancelCurrentAction();
        }

        GD.PrintErr("No action to cancel");
        return false;
    }

    public override void Redo()
    {
        var cellType = history.GetRedoContext<CellType>();

        // If the action we're redoing should be done on another cell type,
        // save our changes to the current cell type, then switch to the other one
        SwapEditingCellIfNeeded(cellType);

        // If the action we're redoing should be done on another editor tab, switch to that tab
        SwapEditorTabIfNeeded(history.ActionToRedo());

        base.Redo();
    }

    public bool IsNewCellTypeNameValid(string newName)
    {
        // Name is invalid if it is empty or a duplicate
        // TODO: should this ensure the name doesn't have trailing whitespace?
        // If so, CellTemplate.UpdateNameIfValid should be updated as well
        return !string.IsNullOrWhiteSpace(newName) && !EditedSpecies.CellTypes.Any(c =>
            c.TypeName.Equals(newName, StringComparison.InvariantCultureIgnoreCase));
    }

    public override void Undo()
    {
        var cellType = history.GetUndoContext<CellType>();

        // If the action we're undoing should be done on another cell type,
        // save our changes to the current cell type, then switch to the other one
        SwapEditingCellIfNeeded(cellType);

        // If the action we're undoing should be done on another editor tab, switch to that tab
        SwapEditorTabIfNeeded(history.ActionToUndo());

        base.Undo();
    }

    protected override void ResolveDerivedTypeNodeReferences()
    {
    }

    protected override void InitEditor(bool fresh)
    {
        patchMapTab.SetMap(CurrentGame.GameWorld.Map);

        base.InitEditor(fresh);

        reportTab.UpdateReportTabPatchSelector();

        if (fresh)
        {
            CurrentGame.SetBool("edited_multicellular", true);
        }
        else
        {
            SendAutoEvoResultsToReportComponent();

            reportTab.UpdateTimeIndicator(CurrentGame.GameWorld.TotalPassedTime);

            reportTab.UpdatePatchDetails(CurrentPatch, TargetPatch);

            reportTab.UpdateEvents(CurrentGame.GameWorld.EventsLog, CurrentGame.GameWorld.TotalPassedTime);
        }

        cellEditorTab.UpdateBackgroundImage(CurrentPatch);

        // TODO: as we are a prototype we don't want to auto save
        wantsToSave = false;
    }

    protected override void InitEditorGUI(bool fresh)
    {
        reportTab.OnNextTab = () => SetEditorTab(EditorTab.PatchMap);
        patchMapTab.OnNextTab = () => SetEditorTab(EditorTab.CellEditor);
        bodyPlanEditorTab.OnFinish = ForwardEditorComponentFinishRequest;
        cellEditorTab.OnNextTab = () => SetEditorTab(EditorTab.CellEditor);
        cellEditorTab.ValidateNewCellTypeName = IsNewCellTypeNameValid;

        foreach (var editorComponent in GetAllEditorComponents())
        {
            editorComponent.Init(this, fresh);
        }
    }

    protected override void OnEnterEditor()
    {
        base.OnEnterEditor();

        if (!IsLoadedFromSave)
            TutorialState.SendEvent(TutorialEventType.EnteredMulticellularEditor, EventArgs.Empty, this);
    }

    protected override void UpdateHistoryCallbackTargets(ActionHistory<EditorAction> actionHistory)
    {
        // See TODO comment in MicrobeEditor.UpdateHistoryCallbackTargets
    }

    protected override IEnumerable<IEditorComponent> GetAllEditorComponents()
    {
        yield return reportTab;
        yield return patchMapTab;
        yield return bodyPlanEditorTab;
        yield return cellEditorTab;
    }

    protected override void OnEditorReady()
    {
        // The base method stores the data, so we just need to update the GUI here (in case of failure)
        var run = CurrentGame.GameWorld.GetAutoEvoRun();

        if (run.Results == null)
        {
            reportTab.DisplayAutoEvoFailure(run.Status);
        }

        base.OnEditorReady();

        reportTab.UpdateTimeIndicator(CurrentGame.GameWorld.TotalPassedTime);

        if (autoEvoResults != null && autoEvoExternal != null)
        {
            UpdateAutoEvoToReportTab();
        }

        reportTab.UpdatePatchDetails(CurrentPatch, TargetPatch);

        reportTab.UpdateEvents(CurrentGame.GameWorld.EventsLog, CurrentGame.GameWorld.TotalPassedTime);

        patchMapTab.UpdatePatchEvents();
    }

    protected override void OnUndoPerformed()
    {
        base.OnUndoPerformed();

        CheckDidActionAffectCellTypes(history.ActionToRedo());
    }

    protected override void OnRedoPerformed()
    {
        base.OnRedoPerformed();

        CheckDidActionAffectCellTypes(history.ActionToUndo());
    }

    protected override void UpdatePatchDetails()
    {
        // Patch events are able to change the stage's background so it needs to be updated here.
        cellEditorTab.UpdateBackgroundImage(CurrentPatch);
    }

    protected override GameProperties StartNewGameForEditor()
    {
        return GameProperties.StartNewMulticellularGame(new WorldGenerationSettings());
    }

    protected override void PerformAutoSave()
    {
        SaveHelper.ShowErrorAboutPrototypeSaving(this);
    }

    protected override void PerformQuickSave()
    {
        SaveHelper.ShowErrorAboutPrototypeSaving(this);
    }

    protected override void SaveGame(string name)
    {
        SaveHelper.ShowErrorAboutPrototypeSaving(this);
    }

    protected override void ApplyEditorTab()
    {
        // Hide all
        reportTab.Hide();
        patchMapTab.Hide();
        bodyPlanEditorTab.Hide();
        cellEditorTab.Hide();
        noCellTypeSelected.Hide();

        // Show selected
        switch (selectedEditorTab)
        {
            case EditorTab.Report:
            {
                reportTab.Show();
                SetEditorObjectVisibility(false);
                reportTab.UpdatePatchDetailsIfNeeded(SelectedPatch ?? CurrentPatch);
                break;
            }

            case EditorTab.PatchMap:
            {
                patchMapTab.Show();
                SetEditorObjectVisibility(false);
                break;
            }

            case EditorTab.CellEditor:
            {
                // This must be set visible before CheckAndApplyCellTypeEdit otherwise it won't update already placed
                // visuals
                bodyPlanEditorTab.Show();

                // If we have an edited cell type, then we can apply those changes when we go back to the main editor
                // tab as that's the only exit point and the point where we actually need to use the edited cell
                // type information
                // TODO: write an explanation here why this needs to be before the visibility adjustment
                // See: https://github.com/Revolutionary-Games/Thrive/pull/3457
                CheckAndApplyCellTypeEdit();

                SetEditorObjectVisibility(true);
                cellEditorTab.SetEditorWorldTabSpecificObjectVisibility(false);
                bodyPlanEditorTab.SetEditorWorldTabSpecificObjectVisibility(true);

                bodyPlanEditorTab.UpdateArrow();
                bodyPlanEditorTab.UpdateCamera();

                break;
            }

            case EditorTab.CellTypeEditor:
            {
                if (selectedCellTypeToEdit == null)
                {
                    // Show the "select a cell type" text if not selected yet instead of the cell editor
                    noCellTypeSelected.Show();
                    SetEditorObjectVisibility(false);
                }
                else
                {
                    cellEditorTab.Show();
                    SetEditorObjectVisibility(true);
                    bodyPlanEditorTab.SetEditorWorldTabSpecificObjectVisibility(false);
                    cellEditorTab.SetEditorWorldTabSpecificObjectVisibility(true);

                    cellEditorTab.UpdateArrow();
                    cellEditorTab.UpdateCamera();
                }

                break;
            }

            default:
                throw new Exception("Invalid editor tab");
        }
    }

    protected override void SetupEditedSpecies()
    {
        var species = (MulticellularSpecies?)CurrentGame.GameWorld.PlayerSpecies;
        editedSpecies = species ?? throw new NullReferenceException("didn't find edited species");

        base.SetupEditedSpecies();
    }

    protected override void OnEditorExitTransitionFinished()
    {
        // Clear the edited cell type to avoid the cell editor applying the changes unnecessarily
        selectedCellTypeToEdit = null;

        base.OnEditorExitTransitionFinished();
    }

    private void UpdateAutoEvoToReportTab()
    {
        if (autoEvoResults == null)
            throw new InvalidOperationException("May not be called without report");

        // This creates a new callable each time, but the garbage amount should be negligible
        reportTab.UpdateAutoEvoResults(autoEvoResults, autoEvoExternal?.ToString() ?? "error",
            () => autoEvoResults.MakeSummary(true));
    }

    private void OnStartEditingCellType(string? name, bool switchTab)
    {
        if (CanCancelAction)
        {
            ToolTipManager.Instance.ShowPopup(Localization.Translate("ACTION_BLOCKED_WHILE_ANOTHER_IN_PROGRESS"),
                1.5f);
            return;
        }

        // If there is a null name, that means there is no selected cell,
        // so clear the selectedCellTypeToEdit and return early
        if (string.IsNullOrEmpty(name))
        {
            selectedCellTypeToEdit = null;
            GD.Print("Cleared editing cell type");
            return;
        }

        var newTypeToEdit = EditedSpecies.CellTypes.First(c => c.TypeName == name);

        // Only reinitialize the editor when required
        if (selectedCellTypeToEdit == null || selectedCellTypeToEdit != newTypeToEdit)
        {
            selectedCellTypeToEdit = newTypeToEdit;

            GD.Print("Start editing cell type: ", selectedCellTypeToEdit.TypeName);

            // Reinitialize the cell editor to be able to edit the new cell type
            cellEditorTab.OnEditorSpeciesSetup(EditedBaseSpecies);
        }

        if (switchTab)
            SetEditorTab(EditorTab.CellTypeEditor);
    }

    private void CheckAndApplyCellTypeEdit()
    {
        if (selectedCellTypeToEdit == null)
            return;

        // TODO: only apply if there were changes
        GD.Print("Applying changes made to cell type: ", selectedCellTypeToEdit.TypeName);

        // Apply any changes made to the selected cell
        FinishEditingSelectedCell();
    }

    /// <summary>
    ///   Detects if a cell edit was done that requires us to re-apply cell graphics
    /// </summary>
    private void CheckDidActionAffectCellTypes(EditorAction? action)
    {
        if (action == null)
        {
            GD.PrintErr("Action to check for cell type changes is null");
            return;
        }

        // We don't need to care while in the cell editor tab
        if (selectedEditorTab == EditorTab.CellTypeEditor)
            return;

        // Or if no type is selected to edit
        if (selectedCellTypeToEdit == null)
            return;

        bool affectedACell = false;

        foreach (var actionData in action.Data)
        {
            switch (actionData)
            {
                case OrganelleMoveActionData:
                case OrganelleRemoveActionData:
                case OrganellePlacementActionData:
                case MembraneActionData:
                case RigidityActionData:
                case NewMicrobeActionData:
                case ColourActionData:
                case OrganelleUpgradeActionData:
                    affectedACell = true;
                    break;
            }

            if (affectedACell)
                break;
        }

        if (affectedACell)
        {
            GD.Print("Undone / redone action affected cell types");
            cellEditorTab.OnFinishEditing(false);

            // cellEditorTab.OnEditorSpeciesSetup(EditedBaseSpecies);
            bodyPlanEditorTab.OnCellTypeEdited(selectedCellTypeToEdit);
        }
    }

    private void FinishEditingSelectedCell()
    {
        if (selectedCellTypeToEdit == null)
            return;

        var oldName = selectedCellTypeToEdit.TypeName;

        cellEditorTab.OnFinishEditing(false);

        // Revert to old name if the name is a duplicate
        if (EditedSpecies.CellTypes.Any(c =>
                c != selectedCellTypeToEdit && c.TypeName == selectedCellTypeToEdit.TypeName))
        {
            GD.Print("Cell editor renamed a cell type to a duplicate name, reverting");
            selectedCellTypeToEdit.TypeName = oldName;
        }

        bodyPlanEditorTab.OnCellTypeEdited(selectedCellTypeToEdit);
    }

    private void SwapEditingCellIfNeeded(CellType? newCell)
    {
        if (selectedCellTypeToEdit == newCell || newCell == null)
            return;

        // If we're switching to a new cell type, apply any changes made to the old one
        if (selectedEditorTab == EditorTab.CellTypeEditor && selectedCellTypeToEdit != null)
            FinishEditingSelectedCell();

        // This fixes complex cases where multiple types are undoing and redoing actions
        selectedCellTypeToEdit = newCell;
        cellEditorTab.OnEditorSpeciesSetup(EditedBaseSpecies);
    }

    private void SwapEditorTabIfNeeded(EditorAction? editorAction)
    {
        if (editorAction == null)
            return;

        var actionData = editorAction.Data.FirstOrDefault();

        var targetTab = actionData switch
        {
            // If the action was performed on a single Cell Type, target the Cell Type Editor tab
            EditorCombinableActionData<CellType> => EditorTab.CellTypeEditor,

            // If the action was performed on the species as a whole, target the Cell Editor tab
            EditorCombinableActionData<MulticellularSpecies> => EditorTab.CellEditor,

            // If the action wasn't performed in any specific context, just stay on the currently selected tab
            _ => selectedEditorTab,
        };

        // If we're already on the selected tab, there's no need to do anything
        if (targetTab == selectedEditorTab)
            return;

        SetEditorTab(targetTab);
    }
}
