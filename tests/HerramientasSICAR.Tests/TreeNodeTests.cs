using HerramientasSICAR.ViewModels;
using System.Collections.Generic;
using System.Linq;
using TiaConnector.Contracts;
using Xunit;

namespace HerramientasSICAR.Tests
{
    public class TreeNodeTests
    {
        private static HmiFolder Folder(string name, params string[] items)
        {
            var folder = new HmiFolder { Name = name };
            folder.Items.AddRange(items);
            return folder;
        }

        private static HmiTree Hmi()
        {
            var screens = Folder("Pantallas", "Inicio");
            screens.Folders.Add(Folder("Traceability", "T_01", "T_02"));
            var hmi = new HmiTree { Name = "HMI_1" };
            hmi.Branches.Add(new HmiBranchTree { Branch = HmiBranch.Screens, Root = screens });
            hmi.Branches.Add(new HmiBranchTree { Branch = HmiBranch.Tags, Root = Folder("Variables", "Tabla") });
            hmi.Branches.Add(new HmiBranchTree { Branch = HmiBranch.Cycles, Root = Folder(null, "Default") });
            return hmi;
        }

        private static TreeNode Child(TreeNode node, string name) => node.Children.Single(h => h.Name == name);

        [Fact]
        public void ReplaceBranches_OneBranch_KeepsChecksAndExpansion()
        {
            var hmi = TreeNode.FromHmi(Hmi(), null);
            var screens = hmi.Children[0];
            var variables = hmi.Children[1];
            var traceability = Child(screens, "Traceability");
            traceability.IsExpanded = true;
            Child(traceability, "T_01").IsChecked = true;

            var added = Folder("Pantallas", "Inicio");
            added.Folders.Add(Folder("Traceability", "T_01", "T_02", "T_03"));
            hmi.ReplaceBranches(new[] { HmiBranch.Screens },
                new List<HmiBranchTree> { new HmiBranchTree { Branch = HmiBranch.Screens, Root = added } });

            Assert.Equal(new HmiBranch?[] { HmiBranch.Screens, HmiBranch.Tags, HmiBranch.Cycles }, hmi.Children.Select(h => h.Branch));
            Assert.Same(variables, hmi.Children[1]);
            var newer = Child(hmi.Children[0], "Traceability");
            Assert.NotSame(traceability, newer);
            Assert.True(newer.IsExpanded);
            Assert.Equal(new[] { "T_01", "T_02", "T_03" }, newer.Children.Select(h => h.Name));
            Assert.Equal(new[] { "T_01" }, hmi.CheckedLeaves().Select(h => h.Name));
            Assert.Null(newer.IsChecked);
            Assert.Null(hmi.IsChecked);
        }

        [Fact]
        public void ReplaceBranches_RemovedAndAdded_KeepsTreeOrder()
        {
            var hmi = TreeNode.FromHmi(Hmi(), null);

            hmi.ReplaceBranches(new[] { HmiBranch.Tags, HmiBranch.Scripts },
                new List<HmiBranchTree> { new HmiBranchTree { Branch = HmiBranch.Scripts, Root = Folder("Scripts", "Script_1") } });

            Assert.Equal(new HmiBranch?[] { HmiBranch.Screens, HmiBranch.Cycles, HmiBranch.Scripts }, hmi.Children.Select(h => h.Branch));
        }

        [Fact]
        public void ReplaceContent_Plc_KeepsChecks()
        {
            var root = new BlockFolder { Name = "Bloques de programa" };
            root.Blocks.Add(new BlockInfo { Name = "Main", Type = "OB", Number = 1 });
            root.Blocks.Add(new BlockInfo { Name = "FB_Viejo", Type = "FB", Number = 10 });
            var plc = TreeNode.FromPlc(new PlcTree { Name = "PLC_1", Root = root }, null);
            Child(plc, "Main").IsChecked = true;

            var newer = new BlockFolder { Name = "Bloques de programa" };
            newer.Blocks.Add(new BlockInfo { Name = "Main", Type = "OB", Number = 1 });
            newer.Blocks.Add(new BlockInfo { Name = "FB_Nuevo", Type = "FB", Number = 11 });
            plc.ReplaceContent(newer);

            Assert.Equal(new[] { "Main", "FB_Nuevo" }, plc.Children.Select(h => h.Name));
            Assert.Equal(new[] { "Main" }, plc.CheckedLeaves().Select(h => h.Name));
            Assert.Null(plc.IsChecked);
        }

        [Fact]
        public void ReplaceBranches_UnreadHmi_MarksRead()
        {
            var hmi = TreeNode.Unread(new DeviceInfo { Name = "HMI_3", Kind = DeviceKind.Hmi }, null);
            Assert.Equal(NodeKind.Hmi, hmi.Kind);
            Assert.False(hmi.IsRead);
            Assert.NotNull(hmi.Detail);
            Assert.Empty(hmi.Children);

            hmi.ReplaceBranches(HmiRules.TreeOrder, Hmi().Branches);

            Assert.True(hmi.IsRead);
            Assert.Null(hmi.Detail);
            Assert.True(hmi.IsExpanded);
            Assert.Equal(new HmiBranch?[] { HmiBranch.Screens, HmiBranch.Tags, HmiBranch.Cycles }, hmi.Children.Select(h => h.Branch));
        }

        [Fact]
        public void ReplaceContent_UnreadPlc_MarksRead()
        {
            var plc = TreeNode.Unread(new DeviceInfo { Name = "PLC_1" }, null);
            var root = new BlockFolder { Name = "Bloques de programa" };
            root.Blocks.Add(new BlockInfo { Name = "Main", Type = "OB", Number = 1 });

            plc.ReplaceContent(root);

            Assert.Equal(NodeKind.Plc, plc.Kind);
            Assert.True(plc.IsRead);
            Assert.Equal(new[] { "Main" }, plc.Children.Select(h => h.Name));
        }

        [Fact]
        public void Find_RereadTree_ReturnsMatchingNode()
        {
            var hmi = TreeNode.FromHmi(Hmi(), null);
            var target = Child(hmi.Children[0], "Traceability");

            hmi.ReplaceBranches(new[] { HmiBranch.Screens }, Hmi().Branches);

            var found = target.Root.Find(target);
            Assert.NotNull(found);
            Assert.NotSame(target, found);
            Assert.Same(hmi, found.Root);
            Assert.Equal(new[] { "Traceability" }, found.Groups);
        }
    }
}
