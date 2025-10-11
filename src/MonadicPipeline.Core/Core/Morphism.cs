namespace LangChainPipeline.Core;

public delegate TB Morphism<in TA, out TB>(TA x);
