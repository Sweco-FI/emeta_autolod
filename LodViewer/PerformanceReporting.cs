using Godot;
using System;

public partial class PerformanceReporting : Node
{
    private Rid viewportRid;
	public double delayTime = 1f; // don't print on every frame, only after this much time has passed

	private double accumulatedTime = 0f;

    public override void _Ready()
	{
		viewportRid = GetTree().Root.GetViewportRid();
		RenderingServer.ViewportSetMeasureRenderTime(viewportRid, true);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		accumulatedTime += delta;

		if (accumulatedTime < delayTime)
			return;

		accumulatedTime = 0f;

		GD.Print($"{GetPrimitiveCount()} {GetDrawCount()} {GetRenderTime()}");
	}

	private double GetRenderTime() => RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewportRid);

	private ulong GetPrimitiveCount() => RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame);
	
	private ulong GetDrawCount() => RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
}
