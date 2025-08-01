(module
    (memory 0 0)
    (memory 1)
    (memory 1)
    (export "memory" (memory 0))
    (type $t0 (func (param i32) (result i32)))
    (func $add_one (export "add_one") (type $t0) (param $p0 i32) (result i32)
        get_local $p0
        i32.const 1
        i32.add)
    (func (export "user_entrypoint") (param $args_len i32) (result i32)
        (i32.const 0)
    ))
