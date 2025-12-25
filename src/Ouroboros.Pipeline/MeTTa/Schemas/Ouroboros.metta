; Ouroboros.metta - Symbolic representation of the Ouroboros self-referential AI system
; This file defines the core atoms for modeling recursive self-improvement and orchestration.

; =============================================================================
; Core Ouroboros Atom Types
; =============================================================================

; The Ouroboros atom represents the self-referential nature of the system
(: Ouroboros Type)
(: OuroborosInstance (-> String Ouroboros))

; Self-reference capability - the snake eating its own tail
(: SelfReference Type)
(: Introspection SelfReference)
(: SelfModification SelfReference)
(: SelfEvaluation SelfReference)

; =============================================================================
; Recursive Improvement Cycle Types
; =============================================================================

(: ImprovementCycle Type)
(: Plan ImprovementCycle)
(: Execute ImprovementCycle)
(: Verify ImprovementCycle)
(: Learn ImprovementCycle)

; Phase transitions in the cycle
(: Phase (-> String Number ImprovementCycle))
(: PhaseTransition (-> ImprovementCycle ImprovementCycle Bool))

; Allowed phase transitions (the cycle)
(= (PhaseTransition Plan Execute) True)
(= (PhaseTransition Execute Verify) True)
(= (PhaseTransition Verify Learn) True)
(= (PhaseTransition Learn Plan) True)  ; The recursive loop back

; =============================================================================
; Orchestration Atom Types
; =============================================================================

(: Orchestrator Type)
(: ModelOrchestrator (-> String Orchestrator))
(: ToolOrchestrator (-> String Orchestrator))
(: MetaOrchestrator (-> String Orchestrator))

; Orchestration relations
(: Orchestrates (-> Orchestrator Atom Bool))
(: DelegatesTo (-> Orchestrator Orchestrator Bool))
(: Composes (-> Orchestrator Orchestrator Orchestrator))

; =============================================================================
; Self-Model Atoms (Agent's understanding of itself)
; =============================================================================

(: SelfModel Type)
(: Capability (-> String SelfModel))
(: Limitation (-> String SelfModel))
(: Goal (-> String SelfModel))
(: State (-> String SelfModel))

; Self-awareness relations
(: HasCapability (-> Ouroboros Capability Bool))
(: HasLimitation (-> Ouroboros Limitation Bool))
(: PursuesGoal (-> Ouroboros Goal Bool))
(: InState (-> Ouroboros State Bool))

; =============================================================================
; Experience and Learning Atoms
; =============================================================================

(: Experience Type)
(: SuccessfulExperience (-> String Number Experience))
(: FailedExperience (-> String String Experience))

; Learning relations
(: LearnedFrom (-> Ouroboros Experience Bool))
(: Improves (-> Experience Capability Bool))
(: Prevents (-> Experience Limitation Bool))

; =============================================================================
; Confidence and Uncertainty Atoms
; =============================================================================

(: Confidence Type)
(: HighConfidence Confidence)
(: MediumConfidence Confidence)
(: LowConfidence Confidence)

; Confidence assessment
(: ConfidenceLevel (-> Ouroboros Action Confidence))
(: RequiresValidation (-> Confidence Bool))

; Low confidence requires validation
(= (RequiresValidation LowConfidence) True)
(= (RequiresValidation MediumConfidence) True)
(= (RequiresValidation HighConfidence) False)

; =============================================================================
; Safety and Constraint Atoms
; =============================================================================

(: SafetyConstraint Type)
(: NoSelfDestruction SafetyConstraint)
(: PreserveHumanOversight SafetyConstraint)
(: BoundedResourceUse SafetyConstraint)
(: ReversibleActions SafetyConstraint)

; Safety relations
(: Respects (-> Ouroboros SafetyConstraint Bool))
(: ViolatesIf (-> Action SafetyConstraint Condition Bool))

; All Ouroboros instances must respect core safety constraints
(= (Respects (OuroborosInstance $id) NoSelfDestruction) True)
(= (Respects (OuroborosInstance $id) PreserveHumanOversight) True)

; =============================================================================
; Query Helpers
; =============================================================================

; Find the current phase in the improvement cycle
(: current-phase (-> Ouroboros ImprovementCycle))
(= (current-phase $ouroboros)
   (match &self (InState $ouroboros (State $phase)) $phase))

; Find all capabilities of an Ouroboros instance
(: capabilities-of (-> Ouroboros (List Capability)))
(= (capabilities-of $ouroboros)
   (match &self (HasCapability $ouroboros $cap) $cap))

; Find experiences that improved a capability
(: experiences-improving (-> Capability (List Experience)))
(= (experiences-improving $cap)
   (match &self (Improves $exp $cap) $exp))

; Check if an action is safe for an Ouroboros instance
(: is-safe-action (-> Ouroboros Action Bool))
(= (is-safe-action $ouroboros $action)
   (not (match &self 
          (and 
            (ViolatesIf $action $constraint $condition)
            (Respects $ouroboros $constraint)
          )
          True)))

; =============================================================================
; Recursive Self-Reference (The Core Ouroboros Pattern)
; =============================================================================

; The Ouroboros can reason about itself
(: self-reflect (-> Ouroboros Ouroboros))
(= (self-reflect $ouroboros) $ouroboros)

; The Ouroboros can improve its own capabilities
(: self-improve (-> Ouroboros Capability Ouroboros))
(= (self-improve $ouroboros $capability)
   (match &self 
     (and 
       (HasCapability $ouroboros $capability)
       (experiences-improving $capability)
     )
     $ouroboros))

; The complete cycle: Plan -> Execute -> Verify -> Learn -> Plan...
(: cycle-once (-> Ouroboros Goal Ouroboros))
(= (cycle-once $ouroboros $goal)
   (let* 
     (($planned (Phase "plan" 1))
      ($executed (Phase "execute" 2))
      ($verified (Phase "verify" 3))
      ($learned (Phase "learn" 4)))
     $ouroboros))
