using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace WatcherCore;

public class GenerateCode(TreeItem treeItem, WatcherSettings settings) {
    private string AssemblyName { get; } = treeItem.FileName;

    public string Generate() {
        StringBuilder builder = new();
        builder.AppendLine("""
            using Microsoft.Xna.Framework.Graphics;
            using ReLogic.Content;
            using Terraria.ModLoader;
            """);
        builder.AppendLine().Append("namespace ").Append(treeItem.FileName).AppendLine(".Resource;").AppendLine();
        //声明静态类声明
        ClassDeclarationSyntax classDeclaration = ClassDeclaration("R").AddModifiers(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.StaticKeyword)
        );

        GenerateClass(ref classDeclaration, treeItem);
        builder.AppendLine(classDeclaration.NormalizeWhitespace(eol:Environment.NewLine).ToFullString());
        return builder.ToString();
    }

    private void GenerateClass(ref ClassDeclarationSyntax parent, TreeItem parentItem) {
        if (parentItem.Directory) {
            if (!parentItem.HasFile())
                return;

            //声明静态类声明
            ClassDeclarationSyntax classDeclaration = ClassDeclaration(parentItem.FileName).AddModifiers(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword));

            //遍历
            foreach (TreeItem item in parentItem.TreeItems)
                GenerateClass(ref classDeclaration, item);

            //添加并更新Parent
            parent = parent.AddMembers(classDeclaration);
            return;
        }
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(parentItem.RelativePath);
        // 拼接目录路径和去掉扩展名的文件名
        var directoryPath = Path.GetDirectoryName(parentItem.RelativePath);
        if (directoryPath == null || fileNameWithoutExtension == null)
            return;
        var resultPath = Path.Combine(directoryPath, fileNameWithoutExtension);

        // 指定 string 类型
        PredefinedTypeSyntax type = PredefinedType(Token(SyntaxKind.StringKeyword));
        string stringValue = $"{AssemblyName}/{resultPath.Replace('\\', '/')}";

        #region 生成 Asset
        string extension = Path.GetExtension(parentItem.FilePath);

        string? assetTypeName = null;
        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)) {
            assetTypeName = "Texture2D";
        }
        else if (string.Equals(extension, ".xnb", StringComparison.OrdinalIgnoreCase)) {
            assetTypeName = "Effect";
        }
        if (assetTypeName != null) {
            TypeSyntax assetType = IdentifierName(assetTypeName);
            assetType = GenericName(Identifier("Asset"), TypeArgumentList(SingletonSeparatedList(assetType)));
            var assetVariable = VariableDeclaration(assetType).WithVariables(
                SingletonSeparatedList(
                    VariableDeclarator(
                        Identifier(
                            GetCSharpFieldName(
                                parentItem.FilePath, true
                            )
                        )
                    )
                    .WithInitializer(
                        EqualsValueClause(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("ModContent"),
                                    IdentifierName($"Request<{assetTypeName}>")
                                )
                            )
                            .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                Literal(stringValue)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );
            parent = parent.AddMembers(FieldDeclaration(assetVariable).AddModifiers(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword)
            ));
        }
        #endregion

        if (settings.GenerateString) {
            // 指定变量名称并设置初始值
            VariableDeclaratorSyntax variableDeclarator = VariableDeclarator(Identifier(GetCSharpFieldName(parentItem.FilePath)))
                .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.StringLiteralExpression,
                    Literal(stringValue))));
            // 创建 VariableDeclarationSyntax
            VariableDeclarationSyntax variableDeclaration = VariableDeclaration(type)
                .WithVariables(SingletonSeparatedList(variableDeclarator));
            // 创建 FieldDeclarationSyntax
            FieldDeclarationSyntax fieldDeclaration = FieldDeclaration(variableDeclaration).AddModifiers(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword));

            //更新
            parent = parent.AddMembers(fieldDeclaration);
        }

    }

    private string GetCSharpFieldName(string path, bool asset = false) {
        // 获取文件名和扩展名
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        // 处理文件名和扩展名
        StringBuilder result = CapitalizeFirstLetter(new(fileName));
        StringBuilder processedExtension = new(extension);
        processedExtension.Replace(".", null);
        CapitalizeFirstLetter(processedExtension);

        // 合并文件名和扩展名
        if (settings.GenerateExtension) {
            if (settings.SnakeCase)
                result.Append('_');
            result.Append(processedExtension);
        }
        if (!asset) {
            result.Append(settings.SnakeCase ? "_String" : "String");
        }

        // 处理 C# 关键字冲突 (添加前缀)
        // if (IsCSharpKeyword(result.ToString()))
        //     result.Insert(0, '_');

        // 如果第一个字符是数字，则添加下划线
        if (char.IsDigit(result[0]))
            result.Insert(0, '_');

        return result.ToString();
    }

    private static StringBuilder CapitalizeFirstLetter(StringBuilder stringBuilder) {
        if (stringBuilder.Length == 0) {
            return stringBuilder;
        }
        stringBuilder[0] = char.ToUpper(stringBuilder[0]);
        return stringBuilder;
    }

#if false
    // 检查字符串是否为 C# 关键字
    private static readonly string[] keywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal",
        "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
        "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
    ];
    private static bool IsCSharpKeyword(string text) {
        return keywords.Contains(text);
    }
#endif
}
