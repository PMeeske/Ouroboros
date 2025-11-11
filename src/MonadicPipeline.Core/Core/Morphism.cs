// <copyright file="Morphism.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core;

public delegate TB Morphism<in TA, out TB>(TA x);
