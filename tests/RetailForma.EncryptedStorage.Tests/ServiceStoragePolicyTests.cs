using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using RetailForma.EncryptedStorage;

namespace RetailForma.EncryptedStorage.Tests;

[SupportedOSPlatform("windows")]
public sealed class ServiceStoragePolicyTests
{
    [Fact]
    public void DescriptorAllowsOnlyDedicatedServiceSystemAndAdministrators()
    {
        var service = new SecurityIdentifier("S-1-5-80-123-234-345-456-567");
        var descriptor = ServiceStoragePolicy.DirectoryDescriptor(service);
        Assert.True(descriptor.AreAccessRulesProtected);
        Assert.Equal("S-1-5-32-544", Assert.IsType<SecurityIdentifier>(descriptor.GetOwner(typeof(SecurityIdentifier))).Value);
        var rules = descriptor.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        Assert.Equal(3, rules.Length);
        Assert.Equal(new[] { "S-1-5-18", "S-1-5-32-544", service.Value }.Order(), rules.Select(r => r.IdentityReference.Value).Order());
        Assert.All(rules, r =>
        {
            Assert.Equal(AccessControlType.Allow, r.AccessControlType);
            Assert.Equal(FileSystemRights.FullControl, r.FileSystemRights);
            Assert.Equal(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, r.InheritanceFlags);
            Assert.False(r.IsInherited);
        });
        Assert.EndsWith(Path.Combine("RetailForma", "Data"), ServiceStoragePolicy.DataDirectory);
        // Does not create ProgramData directories or apply this descriptor to the development PC.
    }

    [Theory]
    [InlineData("S-1-5-80-0")]
    [InlineData("S-1-5-32-545")]
    [InlineData("S-1-1-0")]
    public void BroadGroupsAreNotAcceptedAsTheService(string sid) =>
        Assert.Throws<ArgumentException>(() => ServiceStoragePolicy.DirectoryDescriptor(new SecurityIdentifier(sid)));
}
