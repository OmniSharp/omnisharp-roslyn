using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace OmniSharp.Tests
{
	public class StructureFacts 
	{
		[Fact]
		public async void SimpleClass() 
		{
			var source = 
				@"public class Far {
					
				}";
				
			var workspace = TestHelpers.CreateSimpleWorkspace(source, "d.cs");
			var root = await workspace.GetDocument("d.cs").GetSyntaxRootAsync();
			
			var nodes = StructureComputer.Compute((CSharpSyntaxNode)root);
			Assert.Equal(1, nodes.Count());
			Assert.Equal("Far", nodes.First().Location.Text);
			Assert.Equal(SyntaxKind.ClassDeclaration.ToString(), nodes.First().Kind);
		}
		
		[Fact]
		public async void ClassWithMembers() 
		{
			var source = 
				@"public class Far {
					private bool _b;
					private bool B { get; set; }
					public void M() { }
					public event Action E;
				}";
				
			var workspace = TestHelpers.CreateSimpleWorkspace(source, "d.cs");
			var root = await workspace.GetDocument("d.cs").GetSyntaxRootAsync();
			
			var nodes = StructureComputer.Compute((CSharpSyntaxNode)root);
			Assert.Equal(1, nodes.Count());
			Assert.Equal("Far", nodes.First().Location.Text);
			Assert.Equal(SyntaxKind.ClassDeclaration.ToString(), nodes.First().Kind);
			
			// children
			var children = nodes.First().ChildNodes;
			Assert.Equal(4, children.Count());
			Assert.Equal("_b", children.ElementAt(0).Location.Text);
			Assert.Equal("B", children.ElementAt(1).Location.Text);
			Assert.Equal("M", children.ElementAt(2).Location.Text);
			Assert.Equal("E", children.ElementAt(3).Location.Text);
		}
		
		[Fact]
		public async void SimpleInterface() 
		{
			var source = 
				@"public interface Far {
					
				}";
				
			var workspace = TestHelpers.CreateSimpleWorkspace(source, "d.cs");
			var root = await workspace.GetDocument("d.cs").GetSyntaxRootAsync();
			
			var nodes = StructureComputer.Compute((CSharpSyntaxNode)root);
			Assert.Equal(1, nodes.Count());
			Assert.Equal("Far", nodes.First().Location.Text);
			Assert.Equal(SyntaxKind.InterfaceDeclaration.ToString(), nodes.First().Kind);
		}
	}
}