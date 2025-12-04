#!/usr/bin/env python3
"""
Local Whisper transcription wrapper for MonadicPipeline.
Provides a simple CLI interface for the OpenAI Whisper Python package.

Usage:
    python whisper_transcribe.py <audio_file> [--model MODEL] [--language LANG] [--task TASK] [--output json|text]
"""

import sys
import json
import argparse


def main():
    parser = argparse.ArgumentParser(description="Transcribe audio using local Whisper")
    parser.add_argument("audio_file", help="Path to audio file")
    parser.add_argument("--model", default="base", help="Model size: tiny, base, small, medium, large")
    parser.add_argument("--language", default=None, help="Language code (e.g., en, de, fr)")
    parser.add_argument("--task", default="transcribe", choices=["transcribe", "translate"], help="Task type")
    parser.add_argument("--output", default="json", choices=["json", "text"], help="Output format")
    parser.add_argument("--initial_prompt", default=None, help="Initial prompt for the model")
    
    args = parser.parse_args()
    
    try:
        import whisper
    except ImportError:
        print(json.dumps({"error": "whisper not installed. Run: pip install openai-whisper"}), file=sys.stderr)
        sys.exit(1)
    
    try:
        # Load model
        model = whisper.load_model(args.model)
        
        # Transcribe
        options = {
            "task": args.task,
            "verbose": False,
        }
        
        if args.language:
            options["language"] = args.language
        
        if args.initial_prompt:
            options["initial_prompt"] = args.initial_prompt
        
        result = model.transcribe(args.audio_file, **options)
        
        if args.output == "json":
            output = {
                "text": result["text"].strip(),
                "language": result.get("language", "unknown"),
                "segments": [
                    {
                        "start": seg["start"],
                        "end": seg["end"],
                        "text": seg["text"].strip(),
                    }
                    for seg in result.get("segments", [])
                ]
            }
            print(json.dumps(output, ensure_ascii=False))
        else:
            print(result["text"].strip())
            
    except Exception as e:
        if args.output == "json":
            print(json.dumps({"error": str(e)}))
        else:
            print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
