from hyperon import *
from hyperon.ext import register_atoms
from motto.agents.agent import Agent
import os

# Import openai for Ollama's OpenAI-compatible API
try:
    import openai
except ImportError:
    openai = None


class OllamaAgent(Agent):
    """
    Agent that connects to a local Ollama instance using its OpenAI-compatible API.
    Inherits from motto's Agent so it has the same __metta_call__ behavior.
    """
    _name = "ollama"
    
    def __init__(self, model="llama3", host="http://host.docker.internal:11434"):
        # Don't call super().__init__() with path/code since we're a pure Python agent
        self.model = model
        self.host = host
        self._metta = None
        self._unwrap = True
        # Create OpenAI client pointing to Ollama
        if openai is None:
            raise RuntimeError("openai library is required. pip install openai")
        self.client = openai.OpenAI(
            base_url=f"{host}/v1",
            api_key="ollama"  # Ollama doesn't need a real key
        )
    
    def __call__(self, messages, functions=[]):
        """
        Call the Ollama model with the given messages.
        This receives already-processed messages from Agent.__metta_call__.
        """
        try:
            # Messages come in as a list of dicts with 'role' and 'content' keys
            # after being processed by get_llm_args in Agent.__metta_call__
            formatted_messages = []
            if isinstance(messages, list):
                for msg in messages:
                    if isinstance(msg, dict):
                        formatted_messages.append(msg)
                    elif hasattr(msg, 'role') and hasattr(msg, 'content'):
                        formatted_messages.append({"role": msg.role, "content": msg.content})
            elif isinstance(messages, str):
                formatted_messages.append({"role": "user", "content": messages})
            
            if not formatted_messages:
                formatted_messages = [{"role": "user", "content": str(messages)}]
            
            response = self.client.chat.completions.create(
                model=self.model,
                messages=formatted_messages,
                temperature=0.7,
            )
            return response.choices[0].message
        except Exception as e:
            return f"Error: {e}"


@register_atoms(pass_metta=True)
def ollama_atoms(metta):
    # Use the standard agent_creator_atom pattern like ChatGPTAgent does
    return {
        r"ollama-agent": OllamaAgent.agent_creator_atom(metta)
    }
