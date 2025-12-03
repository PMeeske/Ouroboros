FROM trueagi/hyperon
WORKDIR /app
COPY . /app
# Install dependencies if needed, e.g. requests for ollama
RUN pip install requests httpx openai
RUN pip install -e ./metta-motto
# Create symlink so MeTTa can find motto module
RUN ln -s /app/metta-motto/motto /usr/local/lib/python3.10/site-packages/hyperon/exts/motto
# Create symlink for our custom ollama_agent
RUN ln -s /app/ollama_agent.py /usr/local/lib/python3.10/site-packages/hyperon/exts/ollama_agent.py
ENV PYTHONPATH=/app/metta-motto:/app
