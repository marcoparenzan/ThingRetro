using ThingRetroEmu.Ports;
namespace ThingVCS7800.Tests;
public class LightGunTests
{
    [Fact] public void SensorNeedsBrightLightAtAimAndExpires()
    {
        var gun = new LightGun(); gun.Aim(.5, .5);
        gun.SampleLight(.5, .5, .1); Assert.False(gun.LightDetected);
        gun.SampleLight(.1, .1, 1); Assert.False(gun.LightDetected);
        gun.SampleLight(.5, .5, 1, holdClocks: 2); Assert.True(gun.LightDetected);
        gun.Tick(); Assert.True(gun.LightDetected); gun.Tick(); Assert.False(gun.LightDetected);
    }
    [Fact] public void TriggerIsIndependentAndOffscreenClearsSensor()
    {
        var gun = new LightGun { TriggerPressed = true }; gun.Aim(.5, .5);
        Assert.False(gun.LightDetected); gun.SampleLight(.5, .5, 1);
        gun.Aim(-1, .5); Assert.False(gun.LightDetected); Assert.False(gun.IsOnScreen);
        Assert.True(gun.TriggerPressed); gun.Reset(); Assert.False(gun.TriggerPressed);
    }
    [Fact] public void InvalidCoordinatesRejected()
    {
        var gun = new LightGun(); Assert.Throws<ArgumentOutOfRangeException>(() => gun.Aim(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => gun.SampleLight(0, 0, 1, radius: -1));
    }
}
