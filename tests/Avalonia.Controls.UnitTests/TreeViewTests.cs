// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Data;
using Avalonia.UnitTests;
using Xunit;

namespace Avalonia.Controls.UnitTests
{
    public class TreeViewTests
    {
        [Fact]
        public void Items_Should_Be_Created()
        {
            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                Items = CreateTestTreeData(),
                DataTemplates = CreateNodeDataTemplate(),
            };

            ApplyTemplates(target);

            Assert.Equal(new[] { "Root" }, ExtractItemHeader(target, 0));
            Assert.Equal(new[] { "Child1", "Child2" }, ExtractItemHeader(target, 1));
            Assert.Equal(new[] { "Grandchild2a" }, ExtractItemHeader(target, 2));
        }

        [Fact]
        public void Items_Should_Be_Created_Using_ItemTemplate_If_Present()
        {
            TreeView target;

            var root = new TestRoot
            {
                Child = target = new TreeView
                {
                    Template = CreateTreeViewTemplate(),
                    Items = CreateTestTreeData(),
                    ItemTemplate = new FuncTreeDataTemplate<Node>(
                        _ => new Canvas(),
                        x => x.Children),
                }
            };

            ApplyTemplates(target);

            var items = target.ItemContainerGenerator.Index.Items
                .OfType<TreeViewItem>()
                .ToList();

            Assert.Equal(4, items.Count);
            Assert.All(items, x => Assert.IsType<Canvas>(x.HeaderPresenter.Child));
        }

        [Fact]
        public void Root_ItemContainerGenerator_Containers_Should_Be_Root_Containers()
        {
            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                Items = CreateTestTreeData(),
                DataTemplates = CreateNodeDataTemplate(),
            };

            ApplyTemplates(target);

            var container = (TreeViewItem)target.ItemContainerGenerator.Containers.Single().ContainerControl;
            var header = (TextBlock)container.Header;
            Assert.Equal("Root", header.Text);
        }

        [Fact]
        public void Root_TreeContainerFromItem_Should_Return_Descendent_Item()
        {
            var tree = CreateTestTreeData();
            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                Items = tree,
                DataTemplates = CreateNodeDataTemplate(),
            };

            // For TreeViewItem to find its parent TreeView, OnAttachedToLogicalTree needs
            // to be called, which requires an IStyleRoot.
            var root = new TestRoot();
            root.Child = target;

            ApplyTemplates(target);

            var container = target.ItemContainerGenerator.Index.ContainerFromItem(
                tree[0].Children[1].Children[0]);

            Assert.NotNull(container);

            var header = ((TreeViewItem)container).Header;
            var headerContent = ((TextBlock)header).Text;

            Assert.Equal("Grandchild2a", headerContent);
        }

        [Fact]
        public void Clicking_Item_Should_Select_It()
        {
            var tree = CreateTestTreeData();
            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                Items = tree,
                DataTemplates = CreateNodeDataTemplate(),
            };

            var visualRoot = new TestRoot();
            visualRoot.Child = target;
            ApplyTemplates(target);

            var item = tree[0].Children[1].Children[0];
            var container = (TreeViewItem)target.ItemContainerGenerator.Index.ContainerFromItem(item);

            Assert.NotNull(container);

            container.RaiseEvent(new PointerPressedEventArgs
            {
                RoutedEvent = InputElement.PointerPressedEvent,
                MouseButton = MouseButton.Left,
            });

            Assert.Equal(item, target.SelectedItem);
            Assert.True(container.IsSelected);
        }

        [Fact]
        public void Setting_SelectedItem_Should_Set_Container_Selected()
        {
            var tree = CreateTestTreeData();
            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                Items = tree,
                DataTemplates = CreateNodeDataTemplate(),
            };

            var visualRoot = new TestRoot();
            visualRoot.Child = target;
            ApplyTemplates(target);

            var item = tree[0].Children[1].Children[0];
            var container = (TreeViewItem)target.ItemContainerGenerator.Index.ContainerFromItem(item);

            Assert.NotNull(container);

            target.SelectedItem = item;

            Assert.True(container.IsSelected);
        }

        [Fact]
        public void LogicalChildren_Should_Be_Set()
        {
            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                Items = new[] { "Foo", "Bar", "Baz " },
            };

            ApplyTemplates(target);

            var result = target.GetLogicalChildren()
                .OfType<TreeViewItem>()
                .Select(x => x.Header)
                .OfType<TextBlock>()
                .Select(x => x.Text)
                .ToList();

            Assert.Equal(new[] { "Foo", "Bar", "Baz " }, result);
        }

        [Fact]
        public void Removing_Item_Should_Remove_Itself_And_Children_From_Index()
        {
            var tree = CreateTestTreeData();
            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                DataTemplates = CreateNodeDataTemplate(),
                Items = tree,
            };

            var root = new TestRoot();
            root.Child = target;
            ApplyTemplates(target);

            Assert.Equal(4, target.ItemContainerGenerator.Index.Items.Count());

            tree[0].Children.RemoveAt(1);

            Assert.Equal(2, target.ItemContainerGenerator.Index.Items.Count());
        }

        [Fact]
        public void DataContexts_Should_Be_Correctly_Set()
        {
            var items = new object[]
            {
                "Foo",
                new Node { Value = "Bar" },
                new TextBlock { Text = "Baz" },
                new TreeViewItem { Header = "Qux" },
            };

            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                DataContext = "Base",
                DataTemplates = new DataTemplates
                {
                    new FuncDataTemplate<Node>(x => new Button { Content = x })
                },
                Items = items,
            };

            ApplyTemplates(target);

            var dataContexts = target.Presenter.Panel.Children
                .Cast<Control>()
                .Select(x => x.DataContext)
                .ToList();

            Assert.Equal(
                new object[] { items[0], items[1], "Base", "Base" },
                dataContexts);
        }

        [Fact]
        public void Control_Item_Should_Not_Be_NameScope()
        {
            var items = new object[]
            {
                new TreeViewItem(),
            };

            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                Items = items,
            };

            target.ApplyTemplate();
            target.Presenter.ApplyTemplate();

            var item = target.Presenter.Panel.LogicalChildren[0];
            Assert.Null(NameScope.GetNameScope((TreeViewItem)item));
        }

        [Fact]
        public void DataTemplate_Created_Item_Should_Be_NameScope()
        {
            var items = new object[]
            {
                "foo",
            };

            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                Items = items,
            };

            target.ApplyTemplate();
            target.Presenter.ApplyTemplate();

            var item = target.Presenter.Panel.LogicalChildren[0];
            Assert.NotNull(NameScope.GetNameScope((TreeViewItem)item));
        }

        [Fact]
        public void Should_React_To_Children_Changing()
        {
            var data = CreateTestTreeData();

            var target = new TreeView
            {
                Template = CreateTreeViewTemplate(),
                Items = data,
                DataTemplates = CreateNodeDataTemplate(),
            };

            ApplyTemplates(target);

            Assert.Equal(new[] { "Root" }, ExtractItemHeader(target, 0));
            Assert.Equal(new[] { "Child1", "Child2" }, ExtractItemHeader(target, 1));
            Assert.Equal(new[] { "Grandchild2a" }, ExtractItemHeader(target, 2));

            // Make sure that the binding to Node.Children does not get collected.
            GC.Collect();

            data[0].Children = new AvaloniaList<Node>
            {
                new Node
                {
                    Value = "NewChild1",
                }
            };

            Assert.Equal(new[] { "Root" }, ExtractItemHeader(target, 0));
            Assert.Equal(new[] { "NewChild1" }, ExtractItemHeader(target, 1));
        }

        private void ApplyTemplates(TreeView tree)
        {
            tree.ApplyTemplate();
            tree.Presenter.ApplyTemplate();
            ApplyTemplates(tree.Presenter.Panel.Children);
        }

        private void ApplyTemplates(IEnumerable<IControl> controls)
        {
            foreach (TreeViewItem control in controls)
            {
                control.Template = CreateTreeViewItemTemplate();
                control.ApplyTemplate();
                control.Presenter.ApplyTemplate();
                control.HeaderPresenter.ApplyTemplate();
                ApplyTemplates(control.Presenter.Panel.Children);
            }
        }

        private IList<Node> CreateTestTreeData()
        {
            return new AvaloniaList<Node>
            {
                new Node
                {
                    Value = "Root",
                    Children = new AvaloniaList<Node>
                    {
                        new Node
                        {
                            Value = "Child1",
                        },
                        new Node
                        {
                            Value = "Child2",
                            Children = new AvaloniaList<Node>
                            {
                                new Node
                                {
                                    Value = "Grandchild2a",
                                },
                            },
                        },
                    }
                }
            };
        }

        private DataTemplates CreateNodeDataTemplate()
        {
            return new DataTemplates
            {
                new TestTreeDataTemplate()
            };
        }

        private IControlTemplate CreateTreeViewTemplate()
        {
            return new FuncControlTemplate<TreeView>(parent => new ItemsPresenter
            {
                Name = "PART_ItemsPresenter",
                [~ItemsPresenter.ItemsProperty] = parent[~ItemsControl.ItemsProperty],
            });
        }

        private IControlTemplate CreateTreeViewItemTemplate()
        {
            return new FuncControlTemplate<TreeViewItem>(parent => new Panel
            {
                Children = new Controls
                {
                    new ContentPresenter
                    {
                        Name = "PART_HeaderPresenter",
                        [~ContentPresenter.ContentProperty] = parent[~TreeViewItem.HeaderProperty],
                    },
                    new ItemsPresenter
                    {
                        Name = "PART_ItemsPresenter",
                        [~ItemsPresenter.ItemsProperty] = parent[~ItemsControl.ItemsProperty],
                    }
                }
            });
        }

        private List<string> ExtractItemHeader(TreeView tree, int level)
        {
            return ExtractItemContent(tree.Presenter.Panel, 0, level)
                .Select(x => x.Header)
                .OfType<TextBlock>()
                .Select(x => x.Text)
                .ToList();
        }

        private IEnumerable<TreeViewItem> ExtractItemContent(IPanel panel, int currentLevel, int level)
        {
            foreach (TreeViewItem container in panel.Children)
            {
                if (container.Template == null)
                {
                    container.Template = CreateTreeViewItemTemplate();
                    container.ApplyTemplate();
                }

                if (currentLevel == level)
                {
                    yield return container;
                }
                else
                {
                    foreach (var child in ExtractItemContent(container.Presenter.Panel, currentLevel + 1, level))
                    {
                        yield return child;
                    }
                }
            }
        }

        private class Node :  NotifyingBase
        {
            private IAvaloniaList<Node> _children;

            public string Value { get; set; }

            public IAvaloniaList<Node> Children
            {
                get
                {
                    return _children;
                }

                set
                {
                    _children = value;
                    RaisePropertyChanged(nameof(Children));
                }
            }
        }

        private class TestTreeDataTemplate : ITreeDataTemplate
        {
            public IControl Build(object param)
            {
                var node = (Node)param;
                return new TextBlock { Text = node.Value };
            }

            public InstancedBinding ItemsSelector(object item)
            {
                var obs = new ExpressionObserver(item, nameof(Node.Children));
                return new InstancedBinding(obs);
            }

            public bool Match(object data)
            {
                return data is Node;
            }
        }
    }
}
