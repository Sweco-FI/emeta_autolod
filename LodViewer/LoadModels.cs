using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

public partial class LoadModels : Node
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var containerLod = AddContainer("Range based LOD");
		var containerHigh = AddContainer("High tris");
		var containerLow = AddContainer("Low tris");

		// you can use relative or absolute paths here
		LoadAll(@"../Lodifier/work/clusterer_out", containerLod);
		LoadAll(@"../Lodifier/work/clusterer_out", "cluster_*_lod_0.glb", containerHigh, 0, 1000);
        LoadAll(@"../Lodifier/work/clusterer_out", "cluster_*_lod_3.glb", containerLow, 0, 1000);

        activeContainer = 0;
		ActivateContainer(0);
    }

    private void ActivateContainer(int idx)
    {
		for (int i = 0; i < containers.Count; i++)
		{
			containers[i].Visible = i == idx;
		}

        GD.Print($"Activated {containers[activeContainer].Name}");
    }

    private Node3D AddContainer(string name)
	{
		var container = new Node3D();
		container.Name = name;
		this.AddChild(container);
		containers.Add(container);
		return container;
	}

	private int activeContainer = 0;
	private List<Node3D> containers = new List<Node3D>();

	private void LoadAll(string basePath, Node container)
	{
        LoadAll(basePath, "cluster_*_lod_0.glb", container,  0, 4);
        LoadAll(basePath, "cluster_*_lod_1.glb", container,  4, 10);
        LoadAll(basePath, "cluster_*_lod_2.glb", container, 10, 20);
        LoadAll(basePath, "cluster_*_lod_3.glb", container, 20, 1000);

        GD.Print($"Loaded {basePath}");
    }

    private void LoadAll(string basePath, string fileMatch, Node container, float visRangeBegin, float visRangeEnd)
    {
		foreach (var file in Directory.GetFiles(basePath, fileMatch))
		{
			Load(file, visRangeBegin, visRangeEnd, container);
		}
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{

	}

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);

		if (@event is not InputEvent)
			return;

		if (@event.IsEcho())
			return;

		if (Input.IsKeyPressed(Key.R))
		{
			activeContainer++;
			if (activeContainer >= containers.Count)
				activeContainer = 0;

			ActivateContainer(activeContainer);
		}
    }

    private void Load(string filename, float visRangeBegin, float visRangeEnd, Node container) {
		var doc = new GltfDocument();
		var state = new GltfState();
		var error = doc.AppendFromFile(filename, state);

		if (error != Error.Ok)
		{
			GD.Print($"When trying to load {filename}: {error}");
			return;
		}

		var rootNode = doc.GenerateScene(state);

		var meshes = rootNode.FindChildren("*", "GeometryInstance3D");

		if (meshes == null || meshes.Count == 0)
		{
			GD.Print($"Couldn't find any GeometryInstance3D in the GltfDocument in the file {filename}");
		}

		foreach (var meshNode in meshes)
		{
			// using visibility ranges assumes that the origin point of the meshes is actually in the center of the mesh data
			var mesh = (GeometryInstance3D)meshNode;
			mesh.VisibilityRangeBegin = visRangeBegin;
			mesh.VisibilityRangeEnd = visRangeEnd;
			//mesh.VisibilityRangeBeginMargin = 1f;
			//mesh.VisibilityRangeEndMargin = 1f;
		}

		container.AddChild(rootNode);
	}
}
