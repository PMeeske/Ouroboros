; ToolSignatures.metta - Type signatures for tools enabling backward chaining
; This file defines tool capabilities for neuro-symbolic tool discovery.

; Define Base Types
(: Type Type)
(: Text Type)
(: Summary Type)
(: Code Type)
(: TestResult Type)
(: Document Type)
(: Query Type)
(: Answer Type)
(: Plan Type)
(: VerifiedPlan Type)
(: Embedding Type)
(: SearchResult Type)

; Define Tool Capability Signatures
; Format: (: tool_name (-> InputType OutputType))

; Text processing tools
(: summarize_tool (-> Text Summary))
(: extract_entities_tool (-> Text (List Entity)))
(: translate_tool (-> Text Text))

; Code generation tools
(: generate_code_tool (-> Summary Code))
(: refactor_tool (-> Code Code))
(: optimize_tool (-> Code Code))

; Testing tools
(: run_tests_tool (-> Code TestResult))
(: generate_tests_tool (-> Code Code))
(: analyze_coverage_tool (-> TestResult Summary))

; Document processing tools
(: load_document_tool (-> Query Document))
(: chunk_document_tool (-> Document (List Text)))
(: embed_document_tool (-> Document Embedding))

; Search tools
(: semantic_search_tool (-> Query SearchResult))
(: symbolic_search_tool (-> Query SearchResult))
(: hybrid_search_tool (-> Query SearchResult))

; QA tools
(: answer_question_tool (-> Query Answer))
(: generate_response_tool (-> Summary Answer))

; Planning tools
(: create_plan_tool (-> Query Plan))
(: verify_plan_tool (-> Plan VerifiedPlan))
(: execute_plan_tool (-> VerifiedPlan Answer))

; --- THE CHAINING LOGIC (Backward Chaining) ---

; Base case: Direct tool match
(: solve (-> Type Type Expression))
(= (solve $in $out)
   (match &self 
      (: $tool (-> $in $out)) 
      $tool
   )
)

; Recursive case: Chain through intermediate type
(= (solve $in $out)
   (match &self 
      (: $tool (-> $mid $out))
      (chain (solve $in $mid) $tool)
   )
)

; Helper: Find all tools that accept a given input type
(: tools-accepting (-> Type (List Atom)))
(= (tools-accepting $input_type)
   (match &self (: $tool (-> $input_type $any)) $tool))

; Helper: Find all tools that produce a given output type
(: tools-producing (-> Type (List Atom)))
(= (tools-producing $output_type)
   (match &self (: $tool (-> $any $output_type)) $tool))

; Example query: Find a path from Text to TestResult
; !(solve Text TestResult)
; Expected: (chain (chain summarize_tool generate_code_tool) run_tests_tool)
