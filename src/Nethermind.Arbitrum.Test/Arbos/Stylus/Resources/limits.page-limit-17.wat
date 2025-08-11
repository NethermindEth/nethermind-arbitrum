(module
  ;; Assuming page_limit is something like 16, this would exceed it
  (import "vm_hooks" "pay_for_memory_grow" (func (param i32)))
  (memory 17)
  (export "memory" (memory 0))
  (func (export "user_entrypoint") (param $args_len i32) (result i32)
      (i32.const 0)
  )
)
