Fix usages of InstructionLine.Offset which treat it as an absolute address
   - it is actually offset from start of bank

Fix line numbers usage where 1-based was assumed, and now we've moved to 0-based.
