// 
// ReplaceWithSingleCallToAnyIssue.cs
//
// Author:
//       Mike Kr�ger <mkrueger@xamarin.com>
// 
// Copyright (c) 2013 Xamarin <http://xamarin.com>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using ICSharpCode.NRefactory.PatternMatching;
using ICSharpCode.NRefactory.Refactoring;

namespace ICSharpCode.NRefactory.CSharp.Refactoring
{
    [IssueDescription("Replace with single call to Any(...)",
                      Description = "Replace with single call to Any(...)",
                      Category = IssueCategories.PracticesAndImprovements,
	                  Severity = Severity.Suggestion,
                      ResharperDisableKeyword = "ReplaceWithSingleCallToAny")]
    public class ReplaceWithSingleCallToAnyIssue : ICodeIssueProvider
	{
        static void Main(string[] args)
        {
            /*				switch (member.Name) {
                                case "First":
                                case "FirstOrDefault":
                                case "Last":
                                case "LastOrDefault":
                                case "LongCount":
                                case "Single":
                                case "SingleOrDefault":
                                    return true;
                                default:
                                    return false;
                            }*/
            // ReSharper disable ReplaceWithSingleCallToCount
            Console.WriteLine(args.Where(a => a.Length == 2).Count());
// ReSharper restore ReplaceWithSingleCallToCount
        }

		static readonly AstNode pattern =
			new InvocationExpression (
				new MemberReferenceExpression (
					new NamedNode ("whereInvoke",
					               new InvocationExpression (
					               	new MemberReferenceExpression (new AnyNode ("target"), "Where"),
					               	new AnyNode ())),
					Pattern.AnyString));
		
		public IEnumerable<CodeIssue> GetIssues(BaseRefactoringContext context)
		{
			return new GatherVisitor<ReplaceWithSingleCallToAnyIssue>(context, "Any").GetIssues();
		}
		
		internal class GatherVisitor<T> : GatherVisitorBase<T> where T : ICodeIssueProvider
		{
		    readonly string member;

			public GatherVisitor (BaseRefactoringContext ctx, string member) : base (ctx)
			{
			    this.member = member;
			}
			
			public override void VisitInvocationExpression (InvocationExpression anyInvoke)
			{
				base.VisitInvocationExpression (anyInvoke);
				
				var match = pattern.Match (anyInvoke);
				if (!match.Success)
					return;
				
				var anyResolve = ctx.Resolve (anyInvoke) as InvocationResolveResult;
				if (anyResolve == null || !HasPredicateVersion(anyResolve.Member))
					return;
				var whereInvoke = match.Get<InvocationExpression> ("whereInvoke").Single ();
				var whereResolve = ctx.Resolve (whereInvoke) as InvocationResolveResult;
				if (whereResolve == null || whereResolve.Member.Name != "Where" || !IsQueryExtensionClass(whereResolve.Member.DeclaringTypeDefinition))
					return;
				if (whereResolve.Member.Parameters.Count != 2)
					return;
				var predResolve = whereResolve.Member.Parameters [1];
				if (predResolve.Type.TypeParameterCount != 2)
					return;
				
				AddIssue (
					anyInvoke, string.Format("Redundant Where() call with predicate followed by {0}()", anyResolve.Member.Name),
				    script => {
						var arg = whereInvoke.Arguments.Single ().Clone ();
						var target = match.Get<Expression> ("target").Single ().Clone ();
						script.Replace (anyInvoke, new InvocationExpression (new MemberReferenceExpression (target, anyResolve.Member.Name), arg));
					});
			}
			
			bool IsQueryExtensionClass(ITypeDefinition typeDef)
			{
				if (typeDef == null || typeDef.Namespace != "System.Linq")
					return false;
				switch (typeDef.Name) {
					case "Enumerable":
					case "ParallelEnumerable":
					case "Queryable":
						return true;
					default:
						return false;
				}
			}
			
			bool HasPredicateVersion(IParameterizedMember member)
			{
				if (!IsQueryExtensionClass(member.DeclaringTypeDefinition))
					return false;
			    return member.Name == this.member;
			}
		}
	}
}