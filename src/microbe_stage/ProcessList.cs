﻿using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
///   Shows a list of processes in a container
/// </summary>
public partial class ProcessList : VBoxContainer
{
#pragma warning disable CA2213
    private PackedScene chemicalEquationScene = null!;
#pragma warning restore CA2213

    private float externalSpeedModifier = 1.0f;

    private ChildObjectCache<StrictProcessDisplayInfoEquality, ChemicalEquation> createdProcessControls = null!;
    private List<StrictProcessDisplayInfoEquality>? processesToShow;

    [Signal]
    public delegate void ToggleProcessPressedEventHandler(ChemicalEquation equation);

    public IEnumerable<IProcessDisplayInfo>? ProcessesToShow
    {
        set => processesToShow = value?.Select(d => new StrictProcessDisplayInfoEquality(d)).ToList();
    }

    [Export]
    public bool ShowSpinners { get; set; } = true;

    [Export]
    public bool ShowToggles { get; set; } = true;

    /// <summary>
    ///   The default color for all the process titles in this list. TODO: test that this works still
    /// </summary>
    public LabelSettings? ProcessesTitleColour { get; set; }

    /// <summary>
    ///   External, more technical modifiers for speed, like 2x gameplay speed modifier
    /// </summary>
    public float ExternalSpeedModifier
    {
        get => externalSpeedModifier;

        set
        {
            if (value == externalSpeedModifier)
                return;

            externalSpeedModifier = value;

            UpdateEquationsExternalSpeedModifier();
        }
    }

    /// <summary>
    ///   If true the color of one of the process titles in this list will be changed to red
    ///   if it has any limiting compounds.
    /// </summary>
    public bool MarkRedOnLimitingCompounds { get; set; }

    /// <summary>
    ///   If true the created process objects have <see cref="ChemicalEquation.AutoRefreshProcess"/> set to true
    /// </summary>
    public bool UpdateEquationAutomatically { get; set; } = true;

    public override void _Ready()
    {
        chemicalEquationScene = GD.Load<PackedScene>("res://src/gui_common/ChemicalEquation.tscn");

        // To ensure chemical equations are up to date we use this strict comparison helper here as now normal
        // process equality doesn't take speed into account to make some other parts of the code work much better
        // TODO: would it be ultimately more performant to just let the chemical equations auto update themselves
        // while this is visible? As the comparison operator is pretty expensive for the strict value equality.
        createdProcessControls =
            new ChildObjectCache<StrictProcessDisplayInfoEquality, ChemicalEquation>(this, CreateEquation);

        // To make sure processes refresh when the game is paused
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
            return;

        if (processesToShow == null)
        {
            createdProcessControls.Clear();
            return;
        }

        // Check that all children are up to date
        createdProcessControls.UnMarkAll();

        foreach (var process in processesToShow)
        {
            createdProcessControls.GetChild(process);
        }

        createdProcessControls.ApplyOrder();
        createdProcessControls.DeleteUnmarked();
    }

    private void ClearChildren()
    {
        this.FreeChildren();
    }

    private ChemicalEquation CreateEquation(StrictProcessDisplayInfoEquality process)
    {
        var equation = chemicalEquationScene.Instantiate<ChemicalEquation>();
        equation.ShowSpinner = ShowSpinners;
        equation.ShowToggle = ShowToggles;
        equation.MarkRedOnLimitingCompounds = MarkRedOnLimitingCompounds;
        equation.AutoRefreshProcess = UpdateEquationAutomatically;
        equation.ExternalSpeedModifier = ExternalSpeedModifier;

        // The chemical equation will itself detect when it should pause the spinner, so we don't set this here, as
        // that will break scrolling in the GUI
        // equation.ProcessMode = ProcessModeEnum.Pausable;

        equation.Connect(SignalName.ToggleProcessPressed, new Callable(this, nameof(HandleToggleProcess)));

        if (ProcessesTitleColour != null)
            equation.DefaultTitleFont = ProcessesTitleColour;

        // This creates processes already, so this needs to be done last
        equation.EquationFromProcess = process.DisplayInfo;

        return equation;
    }

    private void UpdateEquationsExternalSpeedModifier()
    {
        // If _Ready was not called yet, don't try to update anything
        if (createdProcessControls == null!)
            return;

        foreach (var equation in createdProcessControls.GetChildren())
        {
            equation.ExternalSpeedModifier = ExternalSpeedModifier;
        }
    }

    private void HandleToggleProcess(ChemicalEquation equation, bool enabled)
    {
        EmitSignal(SignalName.ToggleProcessPressed, equation, enabled);
    }
}
