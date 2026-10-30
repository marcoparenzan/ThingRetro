namespace ThingRetroEmu.Ports;

/// <summary>Machine-independent optical peripheral signals, not a DE-9 electrical pinout.</summary>
public interface ILightGunDevice
{
    double AimX { get; }
    double AimY { get; }
    bool IsOnScreen { get; }
    bool TriggerPressed { get; }
    bool LightDetected { get; }
}

/// <summary>Optical sensor model. A machine adapter supplies raster/light samples and clocks.</summary>
public sealed class LightGun : ILightGunDevice
{
    int remaining;
    public double AimX { get; private set; }
    public double AimY { get; private set; }
    public bool IsOnScreen { get; private set; }
    public bool TriggerPressed { get; set; }
    public bool LightDetected => remaining > 0;
    public void Aim(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
        AimX = x; AimY = y;
        IsOnScreen = x is >= 0 and <= 1 && y is >= 0 and <= 1;
        if (!IsOnScreen) remaining = 0;
    }
    public void SampleLight(double rasterX, double rasterY, double luminance, double radius = .015, int holdClocks = 8)
    {
        if (!double.IsFinite(rasterX) || !double.IsFinite(rasterY) || !double.IsFinite(luminance))
            throw new ArgumentOutOfRangeException(nameof(luminance));
        if (!double.IsFinite(radius) || radius < 0 || holdClocks < 1) throw new ArgumentOutOfRangeException(nameof(radius));
        if (IsOnScreen && luminance >= .5 && Math.Abs(rasterX - AimX) <= radius && Math.Abs(rasterY - AimY) <= radius)
            remaining = holdClocks;
    }
    public void Tick() { if (remaining > 0) remaining--; }
    public void Reset() { remaining = 0; TriggerPressed = false; IsOnScreen = false; }
}
