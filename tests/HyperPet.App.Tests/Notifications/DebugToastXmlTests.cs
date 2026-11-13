using System.Xml.Linq;
using HyperPet.Windows.Notifications;

namespace HyperPet.App.Tests.Notifications;

public sealed class DebugToastXmlTests
{
    [Fact]
    public void BuildToastXml_IsWellFormedToastGeneric_AndEscapesText()
    {
        var toast = XElement.Parse(DebugNotificationSimulator.BuildToastXml("A & B <1>", "say \"hi\""));

        var binding = toast.Element("visual")!.Element("binding")!;
        Assert.Equal("ToastGeneric", binding.Attribute("template")!.Value);
        Assert.Equal(new[] { "A & B <1>", "say \"hi\"" }, binding.Elements("text").Select(text => text.Value));
    }
}
