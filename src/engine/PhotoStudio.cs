﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
///   Creates images of game resources by rendering them in a separate viewport
/// </summary>
[GodotAutoload]
public partial class PhotoStudio : SubViewport
{
    [Export]
    public bool UseBackgroundSceneLoad;

    [Export]
    public bool UseBackgroundSceneInstance;

    [Export]
    public float SimulationWorldTimeAdvanceStep = 1 / 30.0f;

    private static PhotoStudio? instance;

    private readonly PhotoStudioMemoryCache memoryCache = new();

    private readonly Dictionary<ISimulationPhotographable.SimulationType, IWorldSimulation> worldSimulations = new();
    private readonly Dictionary<IWorldSimulation, Node3D> simulationWorldRoots = new();

    private readonly PriorityQueue<ImageTask, (int Priority, int Index)> tasks = new(new TaskComparer());
    private ImageTask? currentTask;
    private Step currentTaskStep = Step.NoTask;

    /// <summary>
    ///   <see cref="PriorityQueue{TElement, TPriority}"/> doesn't guarantee the first-in-first-out order.
    ///   This index offset makes up for that, being increased for each consecutive image task.
    /// </summary>
    private int lastTaskIndex;

    private bool waitingForBackgroundOperation;

#pragma warning disable CA2213

    private DiskCache? diskCache;

    /// <summary>
    ///   This holds the final rendered image across some steps, this is not disposed as this is passed out as the
    ///   result object
    /// </summary>
    private Image? renderedImage;

    private Node3D? instancedScene;

    [Export]
    private Camera3D camera = null!;

    [Export]
    private Node3D renderedObjectHolder = null!;

    [Export]
    private Node simulationWorldsParent = null!;

    private PackedScene? taskScene;

    // This is not disposed as this is contained in a list, the contents of which are disposed
    private IWorldSimulation? previouslyUsedWorldSimulation;
#pragma warning restore CA2213

    private string? loadedTaskScene;
    private bool previousSceneWasCorrect;

    private PhotoStudio() { }

    private enum Step
    {
        NoTask,
        LoadScene,
        InstanceScene,
        ApplySceneParameters,
        AttachScene,
        WaitSceneStabilize,
        PositionCamera,
        Render,
        CaptureImage,
        Save,
        Cleanup,
    }

    public static PhotoStudio Instance => instance ?? throw new InstanceNotLoadedYetException();

    private bool TaskUsesWorldSimulation => currentTask?.SimulationPhotographable != null;

    /// <summary>
    ///   Calculates a good camera distance from the radius of an object that is photographed
    /// </summary>
    /// <param name="radius">The radius of the object</param>
    /// <returns>The distance to use</returns>
    public static float CameraDistanceFromRadiusOfObject(float radius)
    {
        return MathUtils.CameraDistanceFromRadiusOfObject(radius, Constants.PHOTO_STUDIO_CAMERA_FOV);
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
            return;

        instance = this;

        base._Ready();

        // We manually trigger rendering when we want
        RenderTargetUpdateMode = UpdateMode.Disabled;

        camera.Fov = Constants.PHOTO_STUDIO_CAMERA_FOV;

        ProcessMode = ProcessModeEnum.Always;

        diskCache = Settings.Instance.UseDiskCache.Value ? DiskCache.Instance : null;
        Settings.Instance.UseDiskCache.OnChanged += DiskCachingChanged;
    }

    public override void _ExitTree()
    {
        base._ExitTree();

        // Let go of memory resources that were cached as the game should be shutting down
        memoryCache.Clear();

        Settings.Instance.UseDiskCache.OnChanged -= DiskCachingChanged;
    }

    public override void _Process(double delta)
    {
        if (currentTaskStep == Step.NoTask)
        {
            // Probably fine to do cache maintenance slower when generating images
            memoryCache.CleanCacheIfTime(delta);

            // Try to start a task or do nothing if there aren't any
            if (tasks.Count > 0)
            {
                currentTask = tasks.Dequeue();
                currentTaskStep = Step.LoadScene;
                previousSceneWasCorrect = false;

                if (tasks.Count == 0)
                {
                    // Ensuring that the offset doesn't reach int.MaxValue
                    lastTaskIndex = 0;
                }
            }
        }

        switch (currentTaskStep)
        {
            case Step.NoTask:
                break;
            case Step.LoadScene:
            {
                if (TaskUsesWorldSimulation)
                {
                    LoadCurrentTaskWorldSimulation();
                }
                else if (UseBackgroundSceneLoad)
                {
                    if (!waitingForBackgroundOperation)
                    {
                        waitingForBackgroundOperation = true;
                        TaskExecutor.Instance.AddTask(new Task(LoadCurrentTaskScene));
                    }
                }
                else
                {
                    LoadCurrentTaskScene();
                }

                break;
            }

            case Step.InstanceScene:
            {
                if (TaskUsesWorldSimulation)
                {
                    // Make sure the used simulation is visible
                    simulationWorldRoots[previouslyUsedWorldSimulation!].Visible = true;

                    // Remove the old scene if a scene type thing was last photographed
                    loadedTaskScene = null;
                    instancedScene?.QueueFree();
                    instancedScene = null;

                    currentTaskStep = Step.ApplySceneParameters;
                }
                else if (UseBackgroundSceneInstance)
                {
                    if (!waitingForBackgroundOperation)
                    {
                        waitingForBackgroundOperation = true;
                        TaskExecutor.Instance.AddTask(new Task(InstanceCurrentScene));
                    }
                }
                else
                {
                    InstanceCurrentScene();
                }

                break;
            }

            case Step.ApplySceneParameters:
            {
                if (TaskUsesWorldSimulation)
                {
                    currentTask!.SimulationPhotographable!.SetupWorldEntities(previouslyUsedWorldSimulation!);
                }
                else
                {
                    // If a simulation is used, hide that
                    if (previouslyUsedWorldSimulation != null)
                    {
                        simulationWorldRoots[previouslyUsedWorldSimulation].Visible = false;
                        previouslyUsedWorldSimulation = null;
                    }

                    currentTask!.ScenePhotographable!.ApplySceneParameters(instancedScene ??
                        throw new Exception("scene was not instanced when expected"));
                }

                currentTaskStep = Step.AttachScene;
                break;
            }

            case Step.AttachScene:
            {
                if (TaskUsesWorldSimulation)
                {
                    // Run a step to start things happening with the simulation
                    previouslyUsedWorldSimulation!.ProcessLogic(SimulationWorldTimeAdvanceStep);
                }
                else
                {
                    // Only need to swap scenes if the new image is of a different type of thing than what we had
                    // previously
                    if (!previousSceneWasCorrect)
                    {
                        renderedObjectHolder.FreeChildren();
                        renderedObjectHolder.AddChild(instancedScene);
                    }
                }

                currentTaskStep = Step.WaitSceneStabilize;
                break;
            }

            case Step.WaitSceneStabilize:
            {
                if (TaskUsesWorldSimulation)
                {
                    // Wait until simulation no longer has any pending operations
                    if (previouslyUsedWorldSimulation!.HasSystemsWithPendingOperations() ||
                        !currentTask!.SimulationPhotographable!.StateHasStabilized(previouslyUsedWorldSimulation))
                    {
                        previouslyUsedWorldSimulation!.ProcessLogic(SimulationWorldTimeAdvanceStep);
                    }
                    else
                    {
                        // Simulation ready to proceed

                        // Now run the one step with logic frame updates as well in the simulation
                        previouslyUsedWorldSimulation.ProcessAll(SimulationWorldTimeAdvanceStep);

                        currentTaskStep = Step.PositionCamera;
                    }
                }
                else
                {
                    // Need to wait one frame for the objects to initialize
                    currentTaskStep = Step.PositionCamera;
                }

                break;
            }

            case Step.PositionCamera:
            {
                if (TaskUsesWorldSimulation)
                {
                    camera.Position =
                        currentTask!.SimulationPhotographable!.CalculatePhotographDistance(
                            previouslyUsedWorldSimulation!);
                }
                else
                {
                    camera.Position = currentTask!.ScenePhotographable!.CalculatePhotographDistance(instancedScene!);
                }

                currentTaskStep = Step.Render;
                break;
            }

            case Step.Render:
            {
                // Cause a render to happen on this frame from our camera
                RenderTargetUpdateMode = UpdateMode.Once;
                currentTaskStep = Step.CaptureImage;
                break;
            }

            case Step.CaptureImage:
            {
                renderedImage = GetTexture().GetImage();
                currentTaskStep = Step.Save;
                break;
            }

            case Step.Save:
            {
                renderedImage!.Convert(Image.Format.Rgba8);

                // TODO: should mipmaps be optional?
                renderedImage.GenerateMipmaps();

                var texture = ImageTexture.CreateFromImage(renderedImage);

                currentTask!.OnFinished(texture, renderedImage);
                currentTask = null;

                currentTaskStep = Step.Cleanup;
                renderedImage = null;

                break;
            }

            case Step.Cleanup:
            {
                // Cleanup used world simulation (if any)
                previouslyUsedWorldSimulation?.DestroyAllEntities();

                currentTaskStep = Step.NoTask;
                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
        }

        base._Process(delta);
    }

    public IImageTask GenerateImage(IScenePhotographable photographable, int priority = 1)
    {
        var cacheKey = photographable.GetVisualHashCode();

        var image = TryGetFromCache(cacheKey);

        if (image != null)
            return image;

        return HandleTaskSubmit(cacheKey, new ImageTask(photographable, priority));
    }

    public IImageTask GenerateImage(ISimulationPhotographable photographable, int priority = 1)
    {
        var cacheKey = photographable.GetVisualHashCode();

        var image = TryGetFromCache(cacheKey);

        if (image != null)
            return image;

        return HandleTaskSubmit(cacheKey, new ImageTask(photographable, priority));
    }

    public IImageTask? TryGetFromCache(ulong hashCode)
    {
        var image = diskCache != null ? diskCache.Get(hashCode) : memoryCache.Get(hashCode);

        return image;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var entry in worldSimulations)
            {
                entry.Value.Dispose();
            }

            worldSimulations.Clear();

            foreach (var entry in simulationWorldRoots)
            {
                // This is needed to not cause warnings when shutting down the game as apparently the worlds have been
                // destroyed already by Godot
                if (IsInstanceValid(entry.Value))
                    entry.Value.QueueFree();
            }

            simulationWorldRoots.Clear();

            previouslyUsedWorldSimulation = null;
        }

        base.Dispose(disposing);
    }

    private ImageTask HandleTaskSubmit(ulong hash, ImageTask imageTask)
    {
        SubmitTask(imageTask);

        if (diskCache != null)
        {
            diskCache.Insert(hash, imageTask);
        }
        else
        {
            memoryCache.Insert(hash, imageTask);
        }

        return imageTask;
    }

    /// <summary>
    ///   Starts an image creation task. This is now protected as there is a potential to access cache before having
    ///   to create a new task.
    /// </summary>
    /// <param name="task">The task to queue and run as soon as possible</param>
    private void SubmitTask(ImageTask task)
    {
        tasks.Enqueue(task, (task.Priority, lastTaskIndex));
        ++lastTaskIndex;
    }

    private void LoadCurrentTaskScene()
    {
        var wantedScenePath = currentTask!.ScenePhotographable!.SceneToPhotographPath;

        if (wantedScenePath == loadedTaskScene)
        {
            previousSceneWasCorrect = true;
            currentTaskStep = Step.ApplySceneParameters;
        }
        else
        {
            taskScene = GD.Load<PackedScene>(wantedScenePath);
            loadedTaskScene = wantedScenePath;
            previousSceneWasCorrect = false;
            currentTaskStep = Step.InstanceScene;
        }

        waitingForBackgroundOperation = false;
    }

    private void InstanceCurrentScene()
    {
        instancedScene = taskScene!.Instantiate<Node3D>();

        waitingForBackgroundOperation = false;
        currentTaskStep = Step.ApplySceneParameters;
    }

    private void LoadCurrentTaskWorldSimulation()
    {
        var nextSimulation =
            GetOrCreateWorldSimulationForType(currentTask!.SimulationPhotographable!.SimulationToPhotograph);

        if (previouslyUsedWorldSimulation != nextSimulation && previouslyUsedWorldSimulation != null)
        {
            // Switching simulations, hide the previous one
            simulationWorldRoots[previouslyUsedWorldSimulation].Visible = false;
        }

        previouslyUsedWorldSimulation = nextSimulation;
        currentTaskStep = Step.InstanceScene;
    }

    private IWorldSimulation GetOrCreateWorldSimulationForType(ISimulationPhotographable.SimulationType type)
    {
        if (worldSimulations.TryGetValue(type, out var existing))
            return existing;

        switch (type)
        {
            case ISimulationPhotographable.SimulationType.MicrobeGraphics:
            {
                var simulation = new MicrobeVisualOnlySimulation();
                simulation.Init(CreateNewRoot(simulation));

                return worldSimulations[type] = simulation;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private Node3D CreateNewRoot(IWorldSimulation worldSimulation)
    {
        var node = new Node3D();
        simulationWorldsParent.AddChild(node);
        simulationWorldRoots[worldSimulation] = node;

        return node;
    }

    private void DiskCachingChanged(bool value)
    {
        // Clear memory cache when swapping to disk cache
        if (value)
        {
            GD.Print("Clearing memory image cache as disk caching is being enabled");
            memoryCache.Clear();

            diskCache = DiskCache.Instance;
        }
        else
        {
            GD.Print("Disk caching disabled");
            diskCache = null;
        }
    }

    private class TaskComparer : IComparer<(int, int)>
    {
        public int Compare((int, int) x, (int, int) y)
        {
            if (x.Item1 < y.Item1)
                return -1;

            if (x.Item1 > y.Item1)
                return 1;

            return x.Item2.CompareTo(y.Item2);
        }
    }
}
