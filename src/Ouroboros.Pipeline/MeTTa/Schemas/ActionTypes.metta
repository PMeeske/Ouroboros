; ActionTypes.metta - Type system for agent actions
; This file defines the symbolic guard rails for the Ouroboros pipeline.

; Define the Type Hierarchy for Actions
(: Action Type)
(: FileSystemAction (-> String Action))
(: NetworkAction (-> String Action))
(: ToolAction (-> String Action))

; Define Safe Contexts (The Guard Rails)
(: SafeContext Type)
(: ReadOnly SafeContext)
(: FullAccess SafeContext)

; Define the Permission Logic
(: Allowed (-> Action SafeContext Bool))

; --- THE LOGIC ---

; Reading is allowed in ReadOnly context
(= (Allowed (FileSystemAction "read") ReadOnly) True)

; Writing is NOT allowed in ReadOnly context
(= (Allowed (FileSystemAction "write") ReadOnly) False)

; Delete is NOT allowed in ReadOnly context
(= (Allowed (FileSystemAction "delete") ReadOnly) False)

; List is allowed in ReadOnly context
(= (Allowed (FileSystemAction "list") ReadOnly) True)

; Network GET is allowed in ReadOnly context
(= (Allowed (NetworkAction "get") ReadOnly) True)

; Network POST is NOT allowed in ReadOnly context
(= (Allowed (NetworkAction "post") ReadOnly) False)

; Network PUT is NOT allowed in ReadOnly context
(= (Allowed (NetworkAction "put") ReadOnly) False)

; Network DELETE is NOT allowed in ReadOnly context
(= (Allowed (NetworkAction "delete") ReadOnly) False)

; All actions are allowed in FullAccess context
(= (Allowed $action FullAccess) True)

; Default: Unknown actions are NOT allowed in ReadOnly (fail-safe)
(= (Allowed $action ReadOnly) False)
