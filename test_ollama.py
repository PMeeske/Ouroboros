from hyperon import MeTTa

m = MeTTa()
print("Importing motto...")
m.run('!(import! &self motto)')
print("Importing ollama_agent...")
m.run('!(import! &self ollama_agent)')
print("Calling ollama-agent...")
result = m.run('!((ollama-agent "llama3") (user "Hello, how are you?"))')
print("Result:", result)
