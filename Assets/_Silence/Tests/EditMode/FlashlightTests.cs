using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class FlashlightTests
    {
        private static FlashlightModel Lamp(out InventoryModel inventory, double life = 720)
        { inventory = new InventoryModel(); inventory.ConfigureBatteryPacks(4, 2); return new FlashlightModel(inventory, new FlashlightRules(life)); }
        [Test] public void TwelveMinutesOfActualOnTimeDepletesInstalledKit()
        { var m = Lamp(out _); m.Tick(719,719); Assert.That(m.Charge,Is.GreaterThan(0)); m.Tick(720,1); Assert.That(m.BeamOn,Is.False); Assert.That(m.Charge,Is.Zero); }
        [Test] public void OffAndHolsteredDoNotDrain()
        { var m=Lamp(out _); Assert.That(m.Request(FlashlightRequest.Toggle,0),Is.True); m.Tick(.12,0); double charge=m.Charge; m.Tick(300,299.88); Assert.That(m.Charge,Is.EqualTo(charge)); m.Request(FlashlightRequest.Holster,300); m.Tick(301,1); m.Tick(500,199); Assert.That(m.Charge,Is.EqualTo(charge)); }
        [Test] public void FailureReactionRunsOnlyOnceAcrossRepeatedEmptyToggles()
        { var m=Lamp(out _,1); m.Tick(1,1); m.Tick(1.2,.2); Assert.That(m.State,Is.EqualTo(FlashlightState.FailureReaction)); Assert.That(m.Request(FlashlightRequest.Reload,1.2),Is.False); m.Tick(6,4.8); for(int i=0;i<10;i++) { m.Request(FlashlightRequest.Toggle,6+i); m.Tick(6+i,1); } Assert.That(m.ReactionCount,Is.EqualTo(1)); }
        [Test] public void ReloadConsumesAndRefillsOnlyAtInsertionAndRestoresLight()
        { var m=Lamp(out var inv); m.Tick(360,360); Assert.That(m.Request(FlashlightRequest.Reload,360),Is.True); m.Tick(364,4); Assert.That(inv.BatteryPacks,Is.EqualTo(2)); Assert.That(m.Charge,Is.EqualTo(.5)); m.Tick(364.2,.2); Assert.That(inv.BatteryPacks,Is.EqualTo(1)); Assert.That(m.Charge,Is.EqualTo(1)); Assert.That(m.BeamOn,Is.False); m.Tick(366.3,2.1); Assert.That(m.BeamOn,Is.True); Assert.That(inv.BatteryPacks,Is.EqualTo(1)); }
        [Test] public void ReloadWithNoSpareOrFullChargeIsRejected()
        { var m=Lamp(out var inv); Assert.That(m.Request(FlashlightRequest.Reload,0),Is.False); m.Tick(360,360); while(inv.TryConsumeBatteryPack()){} Assert.That(m.Request(FlashlightRequest.Reload,360),Is.False); }
        [Test] public void CancelBeforeInsertPreservesOldChargeAndInventory()
        { var m=Lamp(out var inv); m.Tick(360,360); m.Request(FlashlightRequest.Reload,360); m.Tick(361,1); m.SetSuspended(true,361); m.Tick(370,9); Assert.That(m.Charge,Is.EqualTo(.5)); Assert.That(inv.BatteryPacks,Is.EqualTo(2)); Assert.That(m.BeamOn,Is.False); }
        [Test] public void CancelAfterInsertNeverRefundsOrConsumesTwice()
        { var m=Lamp(out var inv); m.Tick(360,360); m.Request(FlashlightRequest.Reload,360); m.Tick(365,5); m.SetSuspended(true,365); m.Tick(370,5); Assert.That(m.Charge,Is.EqualTo(1)); Assert.That(inv.BatteryPacks,Is.EqualTo(1)); }
        [Test] public void ConcurrentInventoryConsumptionCannotCreateFreeCharge()
        { var m=Lamp(out var inv); m.Tick(360,360); m.Request(FlashlightRequest.Reload,360); while(inv.TryConsumeBatteryPack()){} m.Tick(365,5); Assert.That(m.Charge,Is.EqualTo(.5)); Assert.That(m.State,Is.EqualTo(FlashlightState.Ready)); }
        [Test] public void ReloadWhileSwitchedOffStaysOff()
        { var m=Lamp(out _); m.Tick(100,100); m.Request(FlashlightRequest.Toggle,100); m.Tick(100.2,.2); m.Request(FlashlightRequest.Reload,100.2); m.Tick(107,6.8); Assert.That(m.Charge,Is.EqualTo(1)); Assert.That(m.BeamOn,Is.False); }
        [Test] public void DeadActorDoesNotAutomaticallyReequip()
        { var m=Lamp(out _); m.SetSuspended(true,0,false); m.Tick(10,10); m.SetSuspended(false,10); Assert.That(m.State,Is.EqualTo(FlashlightState.Holstered)); }
        [Test] public void CarrySuspensionResumesThroughEquipTransition()
        { var m=Lamp(out _); m.SetSuspended(true,0); m.Tick(3,3); m.SetSuspended(false,3); Assert.That(m.State,Is.EqualTo(FlashlightState.Equipping)); m.Tick(4,1); Assert.That(m.BeamOn,Is.True); }
        [Test] public void InvalidOrBackwardsClockDoesNotAlterChargeOrInventory()
        { var m=Lamp(out var inv); m.Tick(1,1); double charge=m.Charge; m.Tick(double.NaN,1); m.Tick(0,1); m.Tick(2,double.PositiveInfinity); Assert.That(m.Charge,Is.EqualTo(charge)); Assert.That(inv.BatteryPacks,Is.EqualTo(2)); Assert.That(m.Request(FlashlightRequest.Reload,double.NaN),Is.False); }
        [Test] public void ClaimedDeltaCannotDrainMoreThanElapsedTime()
        { var m=Lamp(out _); m.Tick(1,720); Assert.That(m.Charge,Is.EqualTo(1-1.0/720).Within(1e-8)); }
        [Test] public void SparseTickStillCommitsInsertExactlyOnce()
        { var m=Lamp(out var inv); m.Tick(10,10); m.Request(FlashlightRequest.Reload,10); m.Tick(17,7); m.Tick(17,0); Assert.That(inv.BatteryPacks,Is.EqualTo(1)); Assert.That(m.State,Is.EqualTo(FlashlightState.Ready)); }
        [Test] public void BatteryPouchUsesExistingInventoryWithoutOccupyingMissionSlots()
        { var m=Lamp(out var inv); Assert.That(inv.TryAdd(InventoryModel.FlashlightCellId),Is.True); Assert.That(inv.Items.Count,Is.Zero); inv.TryAdd("battery"); inv.TryConsumeBatteryPack(); Assert.That(inv.Items[0],Is.EqualTo("battery")); Assert.That(inv.TryAddBatteryPack(),Is.True); Assert.That(inv.TryAddBatteryPack(),Is.True); Assert.That(inv.TryAddBatteryPack(),Is.False); }
        [Test] public void PouchConfigurationCannotDuplicateStartingPacks()
        { var m=Lamp(out var inv); inv.TryConsumeBatteryPack(); inv.ConfigureBatteryPacks(4,2); Assert.That(inv.BatteryPacks,Is.EqualTo(1)); Assert.Throws<ArgumentOutOfRangeException>(()=>inv.ApplyBatteryPacksSnapshot(4,5)); }
        [Test] public void EmptyNoticeCannotBeHolsteredIntoAPermanentReloadLock()
        { var m=Lamp(out _,1);m.Tick(1,1);Assert.That(m.Request(FlashlightRequest.Holster,1),Is.False);m.Tick(1.2,.2);m.Tick(6,4.8);Assert.That(m.Request(FlashlightRequest.Reload,6),Is.True); }
        [Test] public void NewBatteryCanDepleteAndReactOnceAgain()
        { var m=Lamp(out _,1);m.Tick(1,1);m.Tick(1.2,.2);m.Tick(6,4.8);Assert.That(m.Request(FlashlightRequest.Reload,6),Is.True);m.Tick(12.3,6.3);m.Tick(13.3,1);m.Tick(13.5,.2);Assert.That(m.ReactionCount,Is.EqualTo(2)); }
        [Test] public void SuspendedRequestsAndReloadHolsterOverlapAreRejected()
        { var m=Lamp(out _);m.Tick(100,100);m.Request(FlashlightRequest.Reload,100);Assert.That(m.Request(FlashlightRequest.Toggle,100.1),Is.False);Assert.That(m.Request(FlashlightRequest.Holster,100.1),Is.False);m.SetSuspended(true,100.1);Assert.That(m.Request(FlashlightRequest.Reload,101),Is.False); }
        [Test] public void InventorySnapshotDoesNotEraseEquipmentConsumables()
        { var m=Lamp(out var inv);inv.ApplySnapshot(3,new[]{"battery","document"});Assert.That(inv.BatteryPacks,Is.EqualTo(2));Assert.That(inv.Items.Count,Is.EqualTo(2)); }
        [Test] public void RuleValidationRejectsInvalidTiming()
        { Assert.Throws<ArgumentOutOfRangeException>(()=>new FlashlightRules(double.NaN));Assert.Throws<ArgumentOutOfRangeException>(()=>new FlashlightRules(reloadSeconds:0));Assert.Throws<ArgumentOutOfRangeException>(()=>new FlashlightRules(insertFraction:.1)); }
        [Test] public void HealthyChargeNeverFlickersAndLowFlickerIsDeterministic()
        { bool dim=false; for(int i=0;i<6000;i++) { double t=i*.01; Assert.That(FlashlightFlicker.Sample(.5,t,17),Is.EqualTo(1)); float a=FlashlightFlicker.Sample(.05,t,17); Assert.That(a,Is.EqualTo(FlashlightFlicker.Sample(.05,t,17))); Assert.That(a,Is.InRange(.28f,1f)); dim|=a<1; } Assert.That(dim,Is.True); Assert.That(FlashlightFlicker.Sample(0,10,17),Is.Zero); }
    }
}
