﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DtoGenerator.Logic.Interfaces;
using DtoGenerator.Logic.Models;
using DtoGenerator.Logic.PropertyMappers;

namespace DtoGenerator.Logic
{
  public class CodeGenerator : ICodeGenerator
  { 
    public string GenerateSourcecode(FileInfo fileInfo)
    {
      var dtoNamespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName($"{fileInfo.Namespace}"));
      foreach (var model in fileInfo.Classes.Where(c => c.IsEnabled))
      {
        var dtoName = $"{model.Name}{Constants.DtoSuffix}";
        var dtoDeclaration = SyntaxFactory.ClassDeclaration(dtoName)
                                          .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

        var fromModelFieldsMapping = new StringBuilder("\n");
        var toModelFieldsMapping = new StringBuilder("\n");
        foreach (var prop in model.Properties.Where(p => p.IsEnabled))
        {
          var setAccessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                          .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
          if (!prop.HasSetter)
            setAccessor = setAccessor.AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

          var propertyDeclaration = SyntaxFactory
                  .PropertyDeclaration(string.IsNullOrWhiteSpace(prop.TypeName) ? prop.Type : SyntaxFactory.ParseTypeName(prop.TypeName), prop.Name)
                  .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                  .AddAccessorListAccessors(
                      SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                      setAccessor);
            
          dtoDeclaration = dtoDeclaration.AddMembers(propertyDeclaration);

          if (model.NeedFromModelMethod)
            fromModelFieldsMapping.Append(prop.Mapper.GetFromModelMappingSyntax());
          if (model.NeedToModelMethod)
            toModelFieldsMapping.Append(prop.Mapper.GetToModelMappingSyntax());
        }
        
        if (model.NeedFromModelMethod)
          dtoDeclaration = dtoDeclaration.AddMembers(createFromModelMethodDeclarationSyntax(fromModelFieldsMapping.ToString(), dtoName, model.Name));
        if (model.NeedToModelMethod)
          dtoDeclaration = dtoDeclaration.AddMembers(createToModelMethodDeclarationSyntax(toModelFieldsMapping.ToString(), model.Name));

        dtoNamespace = dtoNamespace.AddMembers(dtoDeclaration);
      }

      var dtoFile = SyntaxFactory.CompilationUnit()
                                .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(Constants.Using.System)))
                                .AddMembers(dtoNamespace);

      if (fileInfo.Classes.Where(c => c.IsEnabled)
                          .Any(c => c.Properties.Any(p => p.IsGenericType && p.IsEnabled)))
      {
        dtoFile = dtoFile.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(Constants.Using.SystemCollectionsGeneric)));
      }
      if (fileInfo.Classes.Where(c => c.IsEnabled)
        .Any(c => c.Properties.Any(p => p.IsEnumerableType && p.IsEnabled)))
      {
        dtoFile = dtoFile.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(Constants.Using.SystemLinq)));
      }

      return cleanupCodeFormatting(dtoFile.NormalizeWhitespace().ToFullString());
    }

    private string cleanupCodeFormatting(string code)
    {
      var propertyRegex = new Regex(@"\s*{\r\n\s*get;\r\n\s*set;\r\n\s*}");
      var privatePropertyRegEx = new Regex(@"\s*{\r\n\s*get;\r\n\s*private\sset;\r\n\s*}");
      return privatePropertyRegEx.Replace(propertyRegex.Replace(code, " { get; set; }"), " { get; private set; }")
        .Replace(" ;", ";")
        .Replace(" ,", ",")
        .Replace("}; \r\n\r\n", "}; \r\n")
        .Replace("};\r\n\r\n", "};\r\n");
    }

    private MethodDeclarationSyntax createFromModelMethodDeclarationSyntax(string fromModelFieldsMappingString, string dtoName, string modelName)
    {
      var fromModelMethodBody = fromModelFieldsMappingString.Length > 3 ? $"{{{fromModelFieldsMappingString}}};" : "{ };";
      var fromModelMethodDeclaration = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(dtoName), PropertyMapper.FromModelMethodName)
        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
        .AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword))
        .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier(PropertyMapper.FromModelParamName)).WithType(SyntaxFactory.ParseTypeName(modelName)))
        .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement($"return new {dtoName}()"),
          SyntaxFactory.ParseStatement(fromModelMethodBody)));

      return fromModelMethodDeclaration;
    }

    private MethodDeclarationSyntax createToModelMethodDeclarationSyntax(string toModelFieldsMappingString, string modelName)
    {
      var toModelMethodBody = toModelFieldsMappingString.Length > 3 ? $"{{{toModelFieldsMappingString}}};" : "{ };";
      var toModelMethodDeclaration = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(modelName), PropertyMapper.ToModelMethodName)
        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
        .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement($"return new {modelName}()"),
          SyntaxFactory.ParseStatement(toModelMethodBody)));

      return toModelMethodDeclaration;
    }
  }
}
