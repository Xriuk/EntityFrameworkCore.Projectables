using EntityFrameworkCore.Projectables.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.SyntaxRewriters
{
    /// <summary>
    /// Converts methods/properties bodies of hierarchies of classes into typed expressions.
    /// </summary>
    internal class HierarchyMembersConverter
    {
        public ExpressionSyntax DuplicateMethodExpression(IList<INamedTypeSymbol> derivedTypes, ProjectableDescriptor descriptor)
        {
            var @this = SyntaxFactory.IdentifierName("@this");

            var arguments = descriptor.ParametersList?.Parameters.Count > 1 ? ConvertParameters(descriptor.ParametersList) : null;

            // Check if the method has an implementation or if it is abstract, if it is not abstract it will be added
            // as the last result in the if/else if/else chain, otherwise the last type will be used instead
            if (descriptor.ExpressionBody != null)
            {
                // @this is Type1 ? ((Type1)@this).Method(...) : ...
                // ... ? ... :
                // @this is TypeN ? ((TypeN)@this).Method(...) : ...
                // virtualImplementation
                return derivedTypes.Reverse().Aggregate(descriptor.ExpressionBody, AggregateTypes);
            }
            else
            {
                // DEV: handle generic types
                var lastType = derivedTypes[derivedTypes.Count - 1];

                // @this is Type1 ? ((Type1)@this).Method(...) : ...
                // ... ? ... :
                // ((TypeN)@this).Method(...)
                return derivedTypes.Reverse().Skip(1)
                    .Aggregate((ExpressionSyntax)GetMethodInvocationExpression(lastType, descriptor.MemberName!, arguments), AggregateTypes);
            }


            ExpressionSyntax AggregateTypes(ExpressionSyntax expr, INamedTypeSymbol type)
            {
                return SyntaxFactory.ConditionalExpression(
                    SyntaxFactory.BinaryExpression(SyntaxKind.IsExpression, @this, GetTypeName(type)),
                    GetMethodInvocationExpression(type, descriptor.MemberName!, arguments),
                    expr);
            }
        }

        public ExpressionSyntax DuplicatePropertyExpression(IList<INamedTypeSymbol> derivedTypes, ProjectableDescriptor descriptor)
        {
            var @this = SyntaxFactory.IdentifierName("@this");

            // Check if the property has an implementation or if it is abstract, if it is not abstract it will be added
            // as the last result in the if/else if/else chain, otherwise the last type will be used instead
            if (descriptor.ExpressionBody != null)
            {
                // @this is Type1 ? ((Type1)@this).Property : ...
                // ... ? ... :
                // @this is TypeN ? ((TypeN)@this).Property : ...
                // virtualImplementation
                return derivedTypes.Reverse().Aggregate(descriptor.ExpressionBody, AggregateTypes);
            }
            else
            {
                // DEV: handle generic types
                var lastType = derivedTypes[derivedTypes.Count - 1];

                // @this is Type1 ? ((Type1)@this).Property : ...
                // ... ? ... :
                // ((TypeN)@this).Property
                return derivedTypes.Reverse().Skip(1)
                    .Aggregate((ExpressionSyntax)GetPropertyExpression(lastType, descriptor.MemberName!), AggregateTypes);
            }


            ExpressionSyntax AggregateTypes(ExpressionSyntax expr, INamedTypeSymbol type)
            {
                return SyntaxFactory.ConditionalExpression(
                    SyntaxFactory.BinaryExpression(SyntaxKind.IsExpression, @this, GetTypeName(type)),
                    GetPropertyExpression(type, descriptor.MemberName!),
                    expr);
            }
        }

        private static ArgumentListSyntax ConvertParameters(ParameterListSyntax parameters)
        {
            return SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(parameters.Parameters.Skip(1).Select(p => {
                // Extract the name of the parameter (e.g., "myParam")
                ExpressionSyntax identifier = SyntaxFactory.IdentifierName(p.Identifier);

                // Handle parameter modifiers (like 'ref', 'out', or 'in')
                SyntaxToken? refKindKeyword = null;
                if (p.Modifiers.Any(SyntaxKind.RefKeyword))
                    refKindKeyword = SyntaxFactory.Token(SyntaxKind.RefKeyword);
                else if (p.Modifiers.Any(SyntaxKind.OutKeyword))
                    refKindKeyword = SyntaxFactory.Token(SyntaxKind.OutKeyword);
                else if (p.Modifiers.Any(SyntaxKind.InKeyword))
                    refKindKeyword = SyntaxFactory.Token(SyntaxKind.InKeyword);

                // Create the Argument node. If it has a ref/out modifier, pass it along.
                if (refKindKeyword != null)
                {
                    return SyntaxFactory.Argument(null, refKindKeyword.Value, identifier);
                }
                else
                {
                    return SyntaxFactory.Argument(identifier);
                }
            })));
        }

        private static TypeSyntax GetTypeName(INamedTypeSymbol type)
        {
            return SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        private static InvocationExpressionSyntax GetMethodInvocationExpression(INamedTypeSymbol type, string methodName, ArgumentListSyntax? arguments)
        {
            var typeName = GetTypeName(type);

            var method = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(typeName, SyntaxFactory.IdentifierName("@this"))),
                SyntaxFactory.IdentifierName(methodName));

            // ((Type)@this).Method(...) 
            return arguments != null ? SyntaxFactory.InvocationExpression(method, arguments) : SyntaxFactory.InvocationExpression(method);
        }

        private static MemberAccessExpressionSyntax GetPropertyExpression(INamedTypeSymbol type, string propertyName)
        {
            var typeName = GetTypeName(type);

            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(typeName, SyntaxFactory.IdentifierName("@this"))),
                SyntaxFactory.IdentifierName(propertyName));
        }
    }
}
