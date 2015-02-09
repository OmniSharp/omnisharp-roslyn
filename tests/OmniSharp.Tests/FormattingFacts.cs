using System;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OmniSharp.Tests {
	
	public class FormattingFacts 
	{
		[Fact]
		public void FindFormatTargetAtCurly() 
		{
			AssertFormatTargetKind(SyntaxKind.ClassDeclaration, @"class C {}$");
			AssertFormatTargetKind(SyntaxKind.InterfaceDeclaration, @"interface I {}$");
			AssertFormatTargetKind(SyntaxKind.EnumDeclaration, @"enum E {}$");
			AssertFormatTargetKind(SyntaxKind.StructDeclaration, @"struct S {}$");
			AssertFormatTargetKind(SyntaxKind.NamespaceDeclaration, @"namespace N {}$");
			
			AssertFormatTargetKind(SyntaxKind.MethodDeclaration, @"
class C {
	public void M(){}$
}");
			AssertFormatTargetKind(SyntaxKind.ObjectInitializerExpression, @"
class C {
	public void M(){
	
		new T() {
			A = 6,
			B = 7
		}$
	}
}");
			AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
	public void M ()
	{
		for(;;){}$
	}
}");
		}
		
		[Fact]
		public void FindFormatTargetAtSemiColon() 
		{
			
			AssertFormatTargetKind(SyntaxKind.FieldDeclaration, @"
class C {
	private int F;$
}");
			AssertFormatTargetKind(SyntaxKind.LocalDeclarationStatement, @"
class C {
	public void M()
	{
		var a = 1234;$
	}
}");
			AssertFormatTargetKind(SyntaxKind.ReturnStatement, @"
class C {
	public int M()
	{
		return 1234;$
	}
}");
			
			AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
	public void M ()
	{
		for(var i = 0;$)
	}
}");
			AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
	public void M ()
	{
		for(var i = 0;$) {}
	}
}");
			AssertFormatTargetKind(SyntaxKind.ForStatement, @"
class C {
	public void M ()
	{
		for(var i = 0; i < 8;$)
	}
}");
		}
		
		private void AssertFormatTargetKind(SyntaxKind kind, string source) 
		{
			var tuple = GetTreeAndOffset(source);
			var target = Formatting.FindFormatTarget(tuple.Item1, tuple.Item2);
			if(target == null) {
				Assert.Null(kind);
			}
			else 
			{
				Assert.Equal(kind, target.CSharpKind());
			}
		}
		
		private Tuple<SyntaxTree, int> GetTreeAndOffset(string value) 
		{
			var idx = value.IndexOf('$');
			if(idx <= 0) {
				Assert.True(false);
			}
			value = value.Remove(idx, 1);
			idx = idx - 1;
			return Tuple.Create(CSharpSyntaxTree.ParseText(value), idx);
		}
	}
}