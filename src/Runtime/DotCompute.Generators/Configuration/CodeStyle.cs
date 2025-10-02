// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file has been refactored and split into multiple files for better organization.
// The types have been moved to the following locations:
//
// - CodeStyle class: /Configuration/Style/CodeStyle.cs
// - IndentationStyle enum: /Configuration/Style/Enums/IndentationStyle.cs
// - BraceStyle enum: /Configuration/Style/Enums/BraceStyle.cs
// - LineEndingStyle enum: /Configuration/Style/Enums/LineEndingStyle.cs
// - NamingConventions class: /Configuration/Style/Conventions/NamingConventions.cs
// - CommentStyle class: /Configuration/Style/CommentStyle.cs
// - CommentDetailLevel enum: /Configuration/Style/Enums/CommentDetailLevel.cs
//
// Please update your using statements to reference the new namespaces:
// - using DotCompute.Generators.Configuration.Style;
// - using DotCompute.Generators.Configuration.Style.Enums;
// - using DotCompute.Generators.Configuration.Style.Conventions;

// Global aliases for backward compatibility - these will be removed in a future version
global using CodeStyle = DotCompute.Generators.Configuration.Style.CodeStyle;
global using IndentationStyle = DotCompute.Generators.Configuration.Style.Enums.IndentationStyle;
global using BraceStyle = DotCompute.Generators.Configuration.Style.Enums.BraceStyle;
global using LineEndingStyle = DotCompute.Generators.Configuration.Style.Enums.LineEndingStyle;
global using NamingConventions = DotCompute.Generators.Configuration.Style.Conventions.NamingConventions;
global using CommentDetailLevel = DotCompute.Generators.Configuration.Style.Enums.CommentDetailLevel;