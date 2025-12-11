; Relations.metta - Semantic triple relations for the knowledge graph
; This file defines relation types for grounding LLM-extracted knowledge.

; Define Atom Types
(: Atom Type)
(: Doc (-> String Atom))
(: User (-> String Atom))
(: State (-> String Atom))
(: Concept (-> String Atom))
(: Entity (-> String Atom))

; Define Relation Types
(: Relation Type)
(: Author (-> Atom Atom Relation))
(: Status (-> Atom Atom Relation))
(: Topic (-> Atom Atom Relation))
(: Contains (-> Atom Atom Relation))
(: References (-> Atom Atom Relation))
(: DependsOn (-> Atom Atom Relation))
(: CreatedBy (-> Atom Atom Relation))
(: ModifiedBy (-> Atom Atom Relation))
(: Type (-> Atom Atom Relation))

; Pre-defined State atoms
(: Outdated State)
(: Current State)
(: Draft State)
(: Reviewed State)
(: Archived State)

; Pre-defined Concept atoms for common topics
(: Kubernetes Concept)
(: Deployment Concept)
(: Configuration Concept)
(: Security Concept)
(: Performance Concept)
(: Testing Concept)
(: Documentation Concept)
(: API Concept)

; Query helper: Find documents by status
(: find-by-status (-> State (List Atom)))
(= (find-by-status $status)
   (match &self (Status $doc $status) $doc))

; Query helper: Find documents by topic
(: find-by-topic (-> Concept (List Atom)))
(= (find-by-topic $topic)
   (match &self (Topic $doc $topic) $doc))

; Query helper: Find documents by author
(: find-by-author (-> Atom (List Atom)))
(= (find-by-author $author)
   (match &self (Author $doc $author) $doc))

; Compound query: Find documents matching both status and topic
; Example: "Find all outdated documents about Kubernetes"
(: find-by-status-and-topic (-> State Concept (List Atom)))
(= (find-by-status-and-topic $status $topic)
   (match &self 
     (, 
       (Status $doc $status)
       (Topic $doc $topic)
     ) 
     $doc))

; Query helper: Get all metadata for a document
(: get-document-metadata (-> Atom (List Relation)))
(= (get-document-metadata $doc)
   (match &self ($rel $doc $value) ($rel $value)))
