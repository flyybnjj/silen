using Silen.Core;
using Xunit;

namespace Silen.Tests;

public sealed class RestartPlanTests
{
    [Fact]
    public void Nothing_is_scheduled_without_an_explicit_choice_and_consumption_is_once()
    {
        var plan = new RestartPlan();
        plan.Register(["game-mode", "application-gpu"]);
        Assert.Equal(2, plan.AppliedCount);
        Assert.False(plan.TakeDeferredRestart());
        plan.ScheduleOnClose();
        Assert.True(plan.TakeDeferredRestart());
        Assert.False(plan.TakeDeferredRestart());
    }

    [Fact]
    public void Cancellation_and_effects_never_claim_all_changes_need_a_reboot()
    {
        var plan = new RestartPlan();
        plan.Register(["input-filterkeys", "power-mode"]);
        Assert.False(plan.RestartRecommended);
        plan.Register(["power-throttling"]);
        Assert.True(plan.RestartRecommended);
        plan.ScheduleOnClose(); plan.Cancel();
        Assert.False(plan.TakeDeferredRestart());
        Assert.Equal(ChangeActivation.ReopenApplication, RestartPlan.Activation("application-gpu"));
        Assert.Equal(ChangeActivation.NextSignIn, RestartPlan.Activation("startup:entry"));
    }
    [Fact]
    public void Partial_changes_are_reported_separately_and_do_not_count_as_completed()
    {
        var plan = new RestartPlan();
        plan.RegisterPartial(["power-throttling", "service-fax"]);
        Assert.Equal(0, plan.AppliedCount);
        Assert.Equal(2, plan.PartialCount);
        Assert.True(plan.HasChanges);
        Assert.True(plan.RestartRecommended);
        Assert.False(plan.TakeDeferredRestart());
        plan.ScheduleOnClose();
        Assert.True(plan.TakeDeferredRestart());
        Assert.False(plan.TakeDeferredRestart());
    }
    [Fact]
    public void Partial_changes_keep_explicit_cancellation_and_completed_counts_independent()
    {
        var plan = new RestartPlan();
        plan.Register(["game-mode"]);
        plan.RegisterPartial(["service-fax"]);
        Assert.Equal(1, plan.AppliedCount);
        Assert.Equal(1, plan.PartialCount);
        Assert.False(plan.RestartRecommended);
        plan.ScheduleOnClose(); plan.Cancel();
        Assert.False(plan.TakeDeferredRestart());
    }
}
