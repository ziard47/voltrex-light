using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Helpers;

namespace voltrex_light.Tests;

[TestClass]
public class RelayCommandTests
{
    [TestMethod]
    public void RelayCommand_ExecutesAction()
    {
        bool executed = false;
        var cmd = new RelayCommand(() => executed = true);

        Assert.IsTrue(cmd.CanExecute(null));
        cmd.Execute(null);

        Assert.IsTrue(executed);
    }

    [TestMethod]
    public void RelayCommand_CanExecute_RespectsPredicate()
    {
        bool canRun = false;
        var cmd = new RelayCommand(() => { }, () => canRun);

        Assert.IsFalse(cmd.CanExecute(null));

        canRun = true;
        Assert.IsTrue(cmd.CanExecute(null));
    }

    [TestMethod]
    public async Task AsyncRelayCommand_ExecutesAndSetsExecutingFlag()
    {
        bool ran = false;
        var cmd = new AsyncRelayCommand(async () =>
        {
            await Task.Delay(10);
            ran = true;
        });

        Assert.IsFalse(cmd.IsExecuting);
        await cmd.ExecuteAsync();

        Assert.IsTrue(ran);
        Assert.IsFalse(cmd.IsExecuting);
    }

    [TestMethod]
    public void GenericRelayCommand_PassesParameter()
    {
        string? passed = null;
        var cmd = new RelayCommand<string>(val => passed = val);

        cmd.Execute("DiskScope");

        Assert.AreEqual("DiskScope", passed);
    }
}

