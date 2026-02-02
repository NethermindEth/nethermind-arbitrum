(module
  (type (;0;) (func (param i32 i32) (result i32)))
  (type (;1;) (func (param i32 i32 i32) (result i32)))
  (type (;2;) (func (param i32 i32)))
  (type (;3;) (func (result i32)))
  (type (;4;) (func (param i32)))
  (type (;5;) (func (param i32 i32 i32 i32 i32 i32)))
  (type (;6;) (func (param i32 i32 i32 i32 i32)))
  (type (;7;) (func (param i32 i32 i32)))
  (type (;8;) (func))
  (type (;9;) (func (param i32) (result i32)))
  (type (;10;) (func (param i32 i32 i32 i32 i32) (result i32)))
  (type (;11;) (func (param i32 i32 i32 i32)))
  (import "vm_hooks" "msg_reentrant" (func (;0;) (type 3)))
  (import "vm_hooks" "read_args" (func (;1;) (type 4)))
  (import "vm_hooks" "create2" (func (;2;) (type 5)))
  (import "vm_hooks" "create1" (func (;3;) (type 6)))
  (import "vm_hooks" "read_return_data" (func (;4;) (type 1)))
  (import "vm_hooks" "emit_log" (func (;5;) (type 7)))
  (import "vm_hooks" "storage_flush_cache" (func (;6;) (type 4)))
  (import "vm_hooks" "write_result" (func (;7;) (type 2)))
  (import "vm_hooks" "pay_for_memory_grow" (func (;8;) (type 4)))
  (func (;9;) (type 8)
    call 10
    call 11
    unreachable)
  (func (;10;) (type 8)
    i32.const 0
    call 8)
  (func (;11;) (type 8)
    call 19
    unreachable)
  (func (;12;) (type 9) (param i32) (result i32)
    (local i32 i32 i32 i32 i64 i64 i64 i64 i32 i32 i32 i32 i32 i32 i32)
    global.get 0
    i32.const 160
    i32.sub
    local.tee 1
    global.set 0
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  block  ;; label = @8
                    block  ;; label = @9
                      i32.const 0
                      i32.load8_u offset=9228
                      local.tee 2
                      i32.const 2
                      i32.ne
                      br_if 0 (;@9;)
                      i32.const 0
                      call 0
                      local.tee 2
                      i32.store8 offset=9228
                      i32.const 1
                      local.set 3
                      local.get 2
                      i32.eqz
                      br_if 1 (;@8;)
                      br 2 (;@7;)
                    end
                    i32.const 1
                    local.set 3
                    local.get 2
                    i32.const 1
                    i32.and
                    br_if 1 (;@7;)
                  end
                  local.get 0
                  i32.const -1
                  i32.le_s
                  br_if 1 (;@6;)
                  block  ;; label = @8
                    block  ;; label = @9
                      local.get 0
                      i32.eqz
                      br_if 0 (;@9;)
                      i32.const 0
                      i32.load8_u offset=9697
                      drop
                      local.get 0
                      i32.const 1
                      call 13
                      local.tee 4
                      i32.eqz
                      br_if 4 (;@5;)
                      local.get 4
                      call 1
                      local.get 0
                      i32.const 32
                      i32.gt_u
                      br_if 1 (;@8;)
                      i32.const 32
                      local.get 0
                      i32.const -1
                      i32.add
                      i32.const 8348
                      call 14
                      unreachable
                    end
                    i32.const 1
                    call 1
                    call 15
                    unreachable
                  end
                  local.get 4
                  i64.load offset=1 align=1
                  local.set 5
                  local.get 4
                  i64.load offset=9 align=1
                  local.set 6
                  local.get 4
                  i64.load offset=17 align=1
                  local.set 7
                  local.get 4
                  i64.load offset=25 align=1
                  local.set 8
                  local.get 4
                  i32.const 33
                  i32.add
                  local.set 2
                  local.get 0
                  i32.const -33
                  i32.add
                  local.set 3
                  block  ;; label = @8
                    local.get 4
                    i32.load8_u
                    i32.const 2
                    i32.ne
                    local.tee 9
                    br_if 0 (;@8;)
                    local.get 3
                    i32.const 31
                    i32.le_u
                    br_if 7 (;@1;)
                    local.get 1
                    i32.const 8
                    i32.add
                    i32.const 24
                    i32.add
                    local.get 2
                    i32.const 24
                    i32.add
                    i64.load align=1
                    i64.store
                    local.get 1
                    i32.const 8
                    i32.add
                    i32.const 16
                    i32.add
                    local.get 2
                    i32.const 16
                    i32.add
                    i64.load align=1
                    i64.store
                    local.get 1
                    i32.const 8
                    i32.add
                    i32.const 8
                    i32.add
                    local.get 2
                    i32.const 8
                    i32.add
                    i64.load align=1
                    i64.store
                    local.get 1
                    local.get 2
                    i64.load align=1
                    i64.store offset=8
                    local.get 4
                    i32.const 65
                    i32.add
                    local.set 2
                    local.get 0
                    i32.const -65
                    i32.add
                    local.set 3
                  end
                  local.get 1
                  i32.const 72
                  i32.add
                  i32.const 16
                  i32.add
                  i32.const 0
                  i32.store
                  local.get 1
                  i32.const 72
                  i32.add
                  i32.const 8
                  i32.add
                  i64.const 0
                  i64.store
                  local.get 1
                  i64.const 0
                  i64.store offset=72
                  local.get 1
                  i32.const 0
                  i32.store offset=92
                  local.get 1
                  local.get 8
                  i64.const 56
                  i64.shl
                  local.get 8
                  i64.const 65280
                  i64.and
                  i64.const 40
                  i64.shl
                  i64.or
                  local.get 8
                  i64.const 16711680
                  i64.and
                  i64.const 24
                  i64.shl
                  local.get 8
                  i64.const 4278190080
                  i64.and
                  i64.const 8
                  i64.shl
                  i64.or
                  i64.or
                  local.get 8
                  i64.const 8
                  i64.shr_u
                  i64.const 4278190080
                  i64.and
                  local.get 8
                  i64.const 24
                  i64.shr_u
                  i64.const 16711680
                  i64.and
                  i64.or
                  local.get 8
                  i64.const 40
                  i64.shr_u
                  i64.const 65280
                  i64.and
                  local.get 8
                  i64.const 56
                  i64.shr_u
                  i64.or
                  i64.or
                  i64.or
                  local.tee 8
                  i64.store8 offset=127
                  local.get 1
                  local.get 8
                  i64.const 8
                  i64.shr_u
                  i64.store8 offset=126
                  local.get 1
                  local.get 8
                  i64.const 16
                  i64.shr_u
                  i64.store8 offset=125
                  local.get 1
                  local.get 8
                  i64.const 24
                  i64.shr_u
                  i64.store8 offset=124
                  local.get 1
                  local.get 8
                  i64.const 32
                  i64.shr_u
                  i64.store8 offset=123
                  local.get 1
                  local.get 8
                  i64.const 40
                  i64.shr_u
                  i64.store8 offset=122
                  local.get 1
                  local.get 8
                  i64.const 48
                  i64.shr_u
                  i64.store8 offset=121
                  local.get 1
                  local.get 8
                  i64.const 56
                  i64.shr_u
                  i64.store8 offset=120
                  local.get 1
                  local.get 7
                  i64.const 56
                  i64.shl
                  local.get 7
                  i64.const 65280
                  i64.and
                  i64.const 40
                  i64.shl
                  i64.or
                  local.get 7
                  i64.const 16711680
                  i64.and
                  i64.const 24
                  i64.shl
                  local.get 7
                  i64.const 4278190080
                  i64.and
                  i64.const 8
                  i64.shl
                  i64.or
                  i64.or
                  local.get 7
                  i64.const 8
                  i64.shr_u
                  i64.const 4278190080
                  i64.and
                  local.get 7
                  i64.const 24
                  i64.shr_u
                  i64.const 16711680
                  i64.and
                  i64.or
                  local.get 7
                  i64.const 40
                  i64.shr_u
                  i64.const 65280
                  i64.and
                  local.get 7
                  i64.const 56
                  i64.shr_u
                  i64.or
                  i64.or
                  i64.or
                  local.tee 7
                  i64.store8 offset=119
                  local.get 1
                  local.get 7
                  i64.const 8
                  i64.shr_u
                  i64.store8 offset=118
                  local.get 1
                  local.get 7
                  i64.const 16
                  i64.shr_u
                  i64.store8 offset=117
                  local.get 1
                  local.get 7
                  i64.const 24
                  i64.shr_u
                  i64.store8 offset=116
                  local.get 1
                  local.get 7
                  i64.const 32
                  i64.shr_u
                  i64.store8 offset=115
                  local.get 1
                  local.get 7
                  i64.const 40
                  i64.shr_u
                  i64.store8 offset=114
                  local.get 1
                  local.get 7
                  i64.const 48
                  i64.shr_u
                  i64.store8 offset=113
                  local.get 1
                  local.get 7
                  i64.const 56
                  i64.shr_u
                  i64.store8 offset=112
                  local.get 1
                  local.get 6
                  i64.const 56
                  i64.shl
                  local.get 6
                  i64.const 65280
                  i64.and
                  i64.const 40
                  i64.shl
                  i64.or
                  local.get 6
                  i64.const 16711680
                  i64.and
                  i64.const 24
                  i64.shl
                  local.get 6
                  i64.const 4278190080
                  i64.and
                  i64.const 8
                  i64.shl
                  i64.or
                  i64.or
                  local.get 6
                  i64.const 8
                  i64.shr_u
                  i64.const 4278190080
                  i64.and
                  local.get 6
                  i64.const 24
                  i64.shr_u
                  i64.const 16711680
                  i64.and
                  i64.or
                  local.get 6
                  i64.const 40
                  i64.shr_u
                  i64.const 65280
                  i64.and
                  local.get 6
                  i64.const 56
                  i64.shr_u
                  i64.or
                  i64.or
                  i64.or
                  local.tee 6
                  i64.store8 offset=111
                  local.get 1
                  local.get 6
                  i64.const 8
                  i64.shr_u
                  i64.store8 offset=110
                  local.get 1
                  local.get 6
                  i64.const 16
                  i64.shr_u
                  i64.store8 offset=109
                  local.get 1
                  local.get 6
                  i64.const 24
                  i64.shr_u
                  i64.store8 offset=108
                  local.get 1
                  local.get 6
                  i64.const 32
                  i64.shr_u
                  i64.store8 offset=107
                  local.get 1
                  local.get 6
                  i64.const 40
                  i64.shr_u
                  i64.store8 offset=106
                  local.get 1
                  local.get 6
                  i64.const 48
                  i64.shr_u
                  i64.store8 offset=105
                  local.get 1
                  local.get 6
                  i64.const 56
                  i64.shr_u
                  i64.store8 offset=104
                  local.get 1
                  local.get 5
                  i64.const 56
                  i64.shl
                  local.get 5
                  i64.const 65280
                  i64.and
                  i64.const 40
                  i64.shl
                  i64.or
                  local.get 5
                  i64.const 16711680
                  i64.and
                  i64.const 24
                  i64.shl
                  local.get 5
                  i64.const 4278190080
                  i64.and
                  i64.const 8
                  i64.shl
                  i64.or
                  i64.or
                  local.get 5
                  i64.const 8
                  i64.shr_u
                  i64.const 4278190080
                  i64.and
                  local.get 5
                  i64.const 24
                  i64.shr_u
                  i64.const 16711680
                  i64.and
                  i64.or
                  local.get 5
                  i64.const 40
                  i64.shr_u
                  i64.const 65280
                  i64.and
                  local.get 5
                  i64.const 56
                  i64.shr_u
                  i64.or
                  i64.or
                  i64.or
                  local.tee 5
                  i64.store8 offset=103
                  local.get 1
                  local.get 5
                  i64.const 8
                  i64.shr_u
                  i64.store8 offset=102
                  local.get 1
                  local.get 5
                  i64.const 16
                  i64.shr_u
                  i64.store8 offset=101
                  local.get 1
                  local.get 5
                  i64.const 24
                  i64.shr_u
                  i64.store8 offset=100
                  local.get 1
                  local.get 5
                  i64.const 32
                  i64.shr_u
                  i64.store8 offset=99
                  local.get 1
                  local.get 5
                  i64.const 40
                  i64.shr_u
                  i64.store8 offset=98
                  local.get 1
                  local.get 5
                  i64.const 48
                  i64.shr_u
                  i64.store8 offset=97
                  local.get 1
                  local.get 5
                  i64.const 56
                  i64.shr_u
                  i64.store8 offset=96
                  block  ;; label = @8
                    block  ;; label = @9
                      local.get 9
                      br_if 0 (;@9;)
                      local.get 1
                      i32.const 128
                      i32.add
                      i32.const 24
                      i32.add
                      local.get 1
                      i32.const 8
                      i32.add
                      i32.const 24
                      i32.add
                      i64.load
                      i64.store
                      local.get 1
                      i32.const 128
                      i32.add
                      i32.const 16
                      i32.add
                      local.get 1
                      i32.const 8
                      i32.add
                      i32.const 16
                      i32.add
                      i64.load
                      i64.store
                      local.get 1
                      i32.const 128
                      i32.add
                      i32.const 8
                      i32.add
                      local.get 1
                      i32.const 8
                      i32.add
                      i32.const 8
                      i32.add
                      i64.load
                      i64.store
                      local.get 1
                      local.get 1
                      i64.load offset=8
                      i64.store offset=128
                      local.get 2
                      local.get 3
                      local.get 1
                      i32.const 96
                      i32.add
                      local.get 1
                      i32.const 128
                      i32.add
                      local.get 1
                      i32.const 72
                      i32.add
                      local.get 1
                      i32.const 92
                      i32.add
                      call 2
                      br 1 (;@8;)
                    end
                    local.get 2
                    local.get 3
                    local.get 1
                    i32.const 96
                    i32.add
                    local.get 1
                    i32.const 72
                    i32.add
                    local.get 1
                    i32.const 92
                    i32.add
                    call 3
                  end
                  local.get 1
                  i32.load offset=92
                  local.set 9
                  block  ;; label = @8
                    block  ;; label = @9
                      local.get 1
                      i32.const 72
                      i32.add
                      i32.const 9208
                      i32.const 20
                      call 46
                      br_if 0 (;@9;)
                      local.get 9
                      i32.const -1
                      i32.le_s
                      br_if 5 (;@4;)
                      i32.const 1
                      local.set 3
                      block  ;; label = @10
                        local.get 9
                        br_if 0 (;@10;)
                        i32.const 0
                        local.set 10
                        i32.const 1
                        local.set 2
                        local.get 4
                        local.get 0
                        call 16
                        br 2 (;@8;)
                      end
                      i32.const 0
                      i32.load8_u offset=9697
                      drop
                      local.get 9
                      i32.const 1
                      call 13
                      local.tee 2
                      i32.eqz
                      br_if 6 (;@3;)
                      local.get 2
                      i32.const 0
                      local.get 9
                      call 4
                      local.set 10
                      local.get 4
                      local.get 0
                      call 16
                      br 1 (;@8;)
                    end
                    local.get 1
                    i32.const 54
                    i32.add
                    local.get 1
                    i32.load8_u offset=74
                    local.tee 2
                    i32.store8
                    local.get 1
                    i32.const 48
                    i32.add
                    local.tee 3
                    local.get 1
                    i32.const 91
                    i32.add
                    i32.load8_u
                    i32.store8
                    i32.const 0
                    local.set 10
                    local.get 1
                    i32.const 64
                    i32.add
                    i32.const 0
                    i32.store
                    local.get 1
                    local.get 1
                    i32.load16_u offset=72
                    local.tee 9
                    i32.store16 offset=52
                    local.get 1
                    local.get 1
                    i32.load offset=87 align=1
                    i32.store offset=44
                    local.get 1
                    i64.const 0
                    i64.store offset=56
                    local.get 1
                    i32.load offset=75 align=1
                    local.set 11
                    local.get 1
                    i32.load offset=79 align=1
                    local.set 12
                    local.get 1
                    i32.load offset=83 align=1
                    local.set 13
                    local.get 1
                    i32.const 70
                    i32.add
                    local.get 2
                    i32.store8
                    local.get 1
                    local.get 9
                    i32.store16 offset=68
                    local.get 1
                    local.get 13
                    i32.store offset=151 align=1
                    local.get 1
                    local.get 12
                    i32.store offset=147 align=1
                    local.get 1
                    local.get 11
                    i32.store offset=143 align=1
                    local.get 1
                    local.get 1
                    i64.load offset=56
                    i64.store offset=128
                    local.get 1
                    local.get 1
                    i64.load offset=63 align=1
                    i64.store offset=135 align=1
                    local.get 1
                    i32.const 159
                    i32.add
                    local.get 3
                    i32.load8_u
                    i32.store8
                    local.get 1
                    local.get 1
                    i32.load offset=44
                    i32.store offset=155 align=1
                    local.get 1
                    i32.const 0
                    i32.store offset=104
                    local.get 1
                    i64.const 4294967296
                    i64.store offset=96 align=4
                    i32.const 1
                    local.set 14
                    i32.const 0
                    local.set 2
                    i32.const 0
                    local.set 3
                    block  ;; label = @9
                      loop  ;; label = @10
                        block  ;; label = @11
                          block  ;; label = @12
                            local.get 3
                            i32.eqz
                            br_if 0 (;@12;)
                            local.get 3
                            local.get 9
                            i32.ne
                            br_if 1 (;@11;)
                          end
                          local.get 2
                          i32.const 32
                          i32.eq
                          br_if 2 (;@9;)
                          local.get 1
                          i32.const 128
                          i32.add
                          local.get 2
                          i32.add
                          local.set 3
                          local.get 1
                          i32.const 128
                          i32.add
                          local.get 2
                          i32.const 32
                          i32.add
                          local.tee 2
                          i32.add
                          local.set 9
                          br 1 (;@10;)
                        end
                        local.get 3
                        i32.const 1
                        i32.add
                        local.set 15
                        local.get 3
                        i32.load8_u
                        local.set 3
                        block  ;; label = @11
                          local.get 10
                          local.get 1
                          i32.load offset=96
                          i32.ne
                          br_if 0 (;@11;)
                          local.get 1
                          i32.const 96
                          i32.add
                          local.get 10
                          local.get 9
                          local.get 15
                          i32.sub
                          i32.const 1
                          i32.add
                          local.tee 14
                          i32.const -1
                          local.get 14
                          select
                          call 17
                          local.get 1
                          i32.load offset=100
                          local.set 14
                        end
                        local.get 14
                        local.get 10
                        i32.add
                        local.get 3
                        i32.store8
                        local.get 1
                        local.get 10
                        i32.const 1
                        i32.add
                        local.tee 10
                        i32.store offset=104
                        local.get 15
                        local.set 3
                        br 0 (;@10;)
                      end
                    end
                    local.get 1
                    i32.load offset=96
                    local.set 2
                    local.get 1
                    i32.load offset=100
                    local.tee 3
                    local.get 10
                    i32.const 1
                    call 5
                    block  ;; label = @9
                      local.get 2
                      i32.eqz
                      br_if 0 (;@9;)
                      local.get 3
                      local.get 2
                      call 16
                    end
                    i32.const 0
                    local.set 3
                    i32.const 0
                    i32.load8_u offset=9697
                    drop
                    i32.const 20
                    local.set 9
                    i32.const 20
                    i32.const 1
                    call 13
                    local.tee 2
                    i32.eqz
                    br_if 6 (;@2;)
                    local.get 2
                    local.get 1
                    i32.load16_u offset=52
                    i32.store16 align=1
                    local.get 2
                    local.get 13
                    i32.store offset=11 align=1
                    local.get 2
                    local.get 12
                    i32.store offset=7 align=1
                    local.get 2
                    local.get 11
                    i32.store offset=3 align=1
                    local.get 2
                    local.get 1
                    i32.load offset=44
                    i32.store offset=15 align=1
                    local.get 2
                    i32.const 2
                    i32.add
                    local.get 1
                    i32.const 52
                    i32.add
                    i32.const 2
                    i32.add
                    i32.load8_u
                    i32.store8
                    local.get 2
                    i32.const 19
                    i32.add
                    local.get 1
                    i32.const 48
                    i32.add
                    i32.load8_u
                    i32.store8
                    local.get 4
                    local.get 0
                    call 16
                    i32.const 20
                    local.set 10
                  end
                  i32.const 0
                  call 6
                  local.get 2
                  local.get 10
                  call 7
                  local.get 9
                  i32.eqz
                  br_if 0 (;@7;)
                  local.get 2
                  local.get 9
                  call 16
                end
                local.get 1
                i32.const 160
                i32.add
                global.set 0
                local.get 3
                return
              end
              i32.const 0
              local.get 0
              i32.const 9176
              call 18
              unreachable
            end
            i32.const 1
            local.get 0
            i32.const 9176
            call 18
            unreachable
          end
          i32.const 0
          local.get 9
          i32.const 9192
          call 18
          unreachable
        end
        i32.const 1
        local.get 9
        i32.const 9192
        call 18
        unreachable
      end
      i32.const 1
      i32.const 20
      i32.const 8304
      call 18
      unreachable
    end
    i32.const 32
    local.get 3
    i32.const 8364
    call 14
    unreachable)
  (func (;13;) (type 0) (param i32 i32) (result i32)
    local.get 0
    call 39)
  (func (;14;) (type 7) (param i32 i32 i32)
    local.get 0
    local.get 1
    local.get 2
    call 30
    unreachable)
  (func (;15;) (type 8)
    (local i32 i64)
    global.get 0
    i32.const 48
    i32.sub
    local.tee 0
    global.set 0
    local.get 0
    i32.const 0
    i32.store offset=4
    local.get 0
    i32.const 0
    i32.store
    local.get 0
    i32.const 2
    i32.store offset=12
    local.get 0
    i32.const 8508
    i32.store offset=8
    local.get 0
    i64.const 2
    i64.store offset=20 align=4
    local.get 0
    i32.const 1
    i64.extend_i32_u
    i64.const 32
    i64.shl
    local.tee 1
    local.get 0
    i64.extend_i32_u
    i64.or
    i64.store offset=40
    local.get 0
    local.get 1
    local.get 0
    i32.const 4
    i32.add
    i64.extend_i32_u
    i64.or
    i64.store offset=32
    local.get 0
    local.get 0
    i32.const 32
    i32.add
    i32.store offset=16
    local.get 0
    i32.const 8
    i32.add
    i32.const 8332
    call 24
    unreachable)
  (func (;16;) (type 2) (param i32 i32)
    (local i32 i32)
    block  ;; label = @1
      block  ;; label = @2
        local.get 0
        i32.const -4
        i32.add
        i32.load
        local.tee 2
        i32.const -8
        i32.and
        local.tee 3
        i32.const 4
        i32.const 8
        local.get 2
        i32.const 3
        i32.and
        local.tee 2
        select
        local.get 1
        i32.add
        i32.lt_u
        br_if 0 (;@2;)
        block  ;; label = @3
          local.get 2
          i32.eqz
          br_if 0 (;@3;)
          local.get 3
          local.get 1
          i32.const 39
          i32.add
          i32.gt_u
          br_if 2 (;@1;)
        end
        local.get 0
        call 43
        return
      end
      i32.const 8833
      i32.const 46
      i32.const 8880
      call 31
      unreachable
    end
    i32.const 8896
    i32.const 46
    i32.const 8944
    call 31
    unreachable)
  (func (;17;) (type 7) (param i32 i32 i32)
    (local i32 i32 i32)
    global.get 0
    i32.const 32
    i32.sub
    local.tee 3
    global.set 0
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          local.get 1
          local.get 2
          i32.add
          local.tee 2
          local.get 1
          i32.ge_u
          br_if 0 (;@3;)
          i32.const 0
          local.set 4
          br 1 (;@2;)
        end
        i32.const 0
        local.set 4
        block  ;; label = @3
          local.get 2
          local.get 0
          i32.load
          local.tee 5
          i32.const 1
          i32.shl
          local.tee 1
          local.get 2
          local.get 1
          i32.gt_u
          select
          local.tee 1
          i32.const 8
          local.get 1
          i32.const 8
          i32.gt_u
          select
          local.tee 1
          i32.const 0
          i32.ge_s
          br_if 0 (;@3;)
          br 1 (;@2;)
        end
        i32.const 0
        local.set 2
        block  ;; label = @3
          local.get 5
          i32.eqz
          br_if 0 (;@3;)
          local.get 3
          local.get 5
          i32.store offset=28
          local.get 3
          local.get 0
          i32.load offset=4
          i32.store offset=20
          i32.const 1
          local.set 2
        end
        local.get 3
        local.get 2
        i32.store offset=24
        local.get 3
        i32.const 8
        i32.add
        local.get 1
        local.get 3
        i32.const 20
        i32.add
        call 45
        local.get 3
        i32.load offset=8
        i32.const 1
        i32.ne
        br_if 1 (;@1;)
        local.get 3
        i32.load offset=16
        local.set 0
        local.get 3
        i32.load offset=12
        local.set 4
      end
      local.get 4
      local.get 0
      i32.const 9076
      call 18
      unreachable
    end
    local.get 3
    i32.load offset=12
    local.set 2
    local.get 0
    local.get 1
    i32.store
    local.get 0
    local.get 2
    i32.store offset=4
    local.get 3
    i32.const 32
    i32.add
    global.set 0)
  (func (;18;) (type 7) (param i32 i32 i32)
    block  ;; label = @1
      local.get 0
      i32.eqz
      br_if 0 (;@1;)
      local.get 0
      local.get 1
      call 22
      unreachable
    end
    local.get 2
    call 23
    unreachable)
  (func (;19;) (type 8)
    (local i32)
    global.get 0
    i32.const 32
    i32.sub
    local.tee 0
    global.set 0
    local.get 0
    i32.const 1
    i32.store offset=4
    local.get 0
    i32.const 8424
    i32.store
    local.get 0
    i64.const 1
    i64.store offset=12 align=4
    local.get 0
    i32.const 2
    i64.extend_i32_u
    i64.const 32
    i64.shl
    i32.const 8448
    i64.extend_i32_u
    i64.or
    i64.store offset=24
    local.get 0
    local.get 0
    i32.const 24
    i32.add
    i32.store offset=8
    local.get 0
    i32.const 8380
    call 24
    unreachable)
  (func (;20;) (type 2) (param i32 i32)
    local.get 0
    local.get 1
    call 21
    unreachable)
  (func (;21;) (type 2) (param i32 i32)
    unreachable)
  (func (;22;) (type 2) (param i32 i32)
    local.get 1
    local.get 0
    call 20
    unreachable)
  (func (;23;) (type 4) (param i32)
    (local i32)
    global.get 0
    i32.const 32
    i32.sub
    local.tee 1
    global.set 0
    local.get 1
    i32.const 0
    i32.store offset=24
    local.get 1
    i32.const 1
    i32.store offset=12
    local.get 1
    i32.const 8416
    i32.store offset=8
    local.get 1
    i64.const 4
    i64.store offset=16 align=4
    local.get 1
    i32.const 8
    i32.add
    local.get 0
    call 24
    unreachable)
  (func (;24;) (type 2) (param i32 i32)
    (local i32)
    global.get 0
    i32.const 16
    i32.sub
    local.tee 2
    global.set 0
    local.get 2
    i32.const 1
    i32.store16 offset=12
    local.get 2
    local.get 1
    i32.store offset=8
    local.get 2
    local.get 0
    i32.store offset=4
    local.get 2
    i32.const 4
    i32.add
    call 27
    unreachable)
  (func (;25;) (type 0) (param i32 i32) (result i32)
    local.get 0
    i32.load
    local.get 1
    call 26)
  (func (;26;) (type 0) (param i32 i32) (result i32)
    (local i32 i32 i32 i32 i32 i32 i32 i32)
    global.get 0
    i32.const 16
    i32.sub
    local.tee 2
    global.set 0
    i32.const 10
    local.set 3
    local.get 0
    local.set 4
    block  ;; label = @1
      local.get 0
      i32.const 1000
      i32.lt_u
      br_if 0 (;@1;)
      i32.const 10
      local.set 3
      local.get 0
      local.set 5
      loop  ;; label = @2
        local.get 2
        i32.const 6
        i32.add
        local.get 3
        i32.add
        local.tee 6
        i32.const -3
        i32.add
        local.get 5
        local.get 5
        i32.const 10000
        i32.div_u
        local.tee 4
        i32.const 10000
        i32.mul
        i32.sub
        local.tee 7
        i32.const 65535
        i32.and
        i32.const 100
        i32.div_u
        local.tee 8
        i32.const 1
        i32.shl
        local.tee 9
        i32.const 8525
        i32.add
        i32.load8_u
        i32.store8
        local.get 6
        i32.const -4
        i32.add
        local.get 9
        i32.const 8524
        i32.add
        i32.load8_u
        i32.store8
        local.get 6
        i32.const -1
        i32.add
        local.get 7
        local.get 8
        i32.const 100
        i32.mul
        i32.sub
        i32.const 65535
        i32.and
        i32.const 1
        i32.shl
        local.tee 7
        i32.const 8525
        i32.add
        i32.load8_u
        i32.store8
        local.get 6
        i32.const -2
        i32.add
        local.get 7
        i32.const 8524
        i32.add
        i32.load8_u
        i32.store8
        local.get 3
        i32.const -4
        i32.add
        local.set 3
        local.get 5
        i32.const 9999999
        i32.gt_u
        local.set 6
        local.get 4
        local.set 5
        local.get 6
        br_if 0 (;@2;)
      end
    end
    block  ;; label = @1
      block  ;; label = @2
        local.get 4
        i32.const 9
        i32.gt_u
        br_if 0 (;@2;)
        local.get 4
        local.set 5
        br 1 (;@1;)
      end
      local.get 2
      i32.const 6
      i32.add
      local.get 3
      i32.add
      i32.const -1
      i32.add
      local.get 4
      local.get 4
      i32.const 65535
      i32.and
      i32.const 100
      i32.div_u
      local.tee 5
      i32.const 100
      i32.mul
      i32.sub
      i32.const 65535
      i32.and
      i32.const 1
      i32.shl
      local.tee 6
      i32.const 8525
      i32.add
      i32.load8_u
      i32.store8
      local.get 2
      i32.const 6
      i32.add
      local.get 3
      i32.const -2
      i32.add
      local.tee 3
      i32.add
      local.get 6
      i32.const 8524
      i32.add
      i32.load8_u
      i32.store8
    end
    block  ;; label = @1
      block  ;; label = @2
        local.get 0
        i32.eqz
        br_if 0 (;@2;)
        local.get 5
        i32.eqz
        br_if 1 (;@1;)
      end
      local.get 2
      i32.const 6
      i32.add
      local.get 3
      i32.const -1
      i32.add
      local.tee 3
      i32.add
      local.get 5
      i32.const 1
      i32.shl
      i32.const 30
      i32.and
      i32.const 8525
      i32.add
      i32.load8_u
      i32.store8
    end
    local.get 1
    i32.const 1
    i32.const 0
    local.get 2
    i32.const 6
    i32.add
    local.get 3
    i32.add
    i32.const 10
    local.get 3
    i32.sub
    call 28
    local.set 5
    local.get 2
    i32.const 16
    i32.add
    global.set 0
    local.get 5)
  (func (;27;) (type 4) (param i32)
    (local i32 i64)
    global.get 0
    i32.const 16
    i32.sub
    local.tee 1
    global.set 0
    local.get 0
    i64.load align=4
    local.set 2
    local.get 1
    local.get 0
    i32.store offset=12
    local.get 1
    local.get 2
    i64.store offset=4 align=4
    local.get 1
    i32.const 4
    i32.add
    call 36
    unreachable)
  (func (;28;) (type 10) (param i32 i32 i32 i32 i32) (result i32)
    (local i32 i32 i32 i32 i32 i32 i32 i64)
    local.get 0
    i32.load offset=8
    local.tee 5
    i32.const 2097152
    i32.and
    local.tee 6
    i32.const 21
    i32.shr_u
    local.get 4
    i32.add
    local.set 7
    block  ;; label = @1
      block  ;; label = @2
        local.get 5
        i32.const 8388608
        i32.and
        br_if 0 (;@2;)
        i32.const 0
        local.set 1
        br 1 (;@1;)
      end
      block  ;; label = @2
        block  ;; label = @3
          local.get 2
          br_if 0 (;@3;)
          i32.const 0
          local.set 8
          br 1 (;@2;)
        end
        local.get 1
        i32.load8_s
        i32.const -65
        i32.gt_s
        local.set 8
        local.get 2
        i32.const 1
        i32.eq
        br_if 0 (;@2;)
        local.get 8
        local.get 1
        i32.load8_s offset=1
        i32.const -65
        i32.gt_s
        i32.add
        local.set 8
      end
      local.get 8
      local.get 7
      i32.add
      local.set 7
    end
    i32.const 43
    i32.const 1114112
    local.get 6
    select
    local.set 9
    block  ;; label = @1
      block  ;; label = @2
        local.get 7
        local.get 0
        i32.load16_u offset=12
        local.tee 10
        i32.ge_u
        br_if 0 (;@2;)
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              local.get 5
              i32.const 16777216
              i32.and
              br_if 0 (;@5;)
              local.get 10
              local.get 7
              i32.sub
              local.set 10
              i32.const 0
              local.set 6
              i32.const 0
              local.set 11
              block  ;; label = @6
                block  ;; label = @7
                  block  ;; label = @8
                    local.get 5
                    i32.const 29
                    i32.shr_u
                    i32.const 3
                    i32.and
                    br_table 2 (;@6;) 0 (;@8;) 1 (;@7;) 0 (;@8;) 2 (;@6;)
                  end
                  local.get 10
                  local.set 11
                  br 1 (;@6;)
                end
                local.get 10
                i32.const 65534
                i32.and
                i32.const 1
                i32.shr_u
                local.set 11
              end
              local.get 5
              i32.const 2097151
              i32.and
              local.set 8
              local.get 0
              i32.load offset=4
              local.set 7
              local.get 0
              i32.load
              local.set 0
              loop  ;; label = @6
                local.get 6
                i32.const 65535
                i32.and
                local.get 11
                i32.const 65535
                i32.and
                i32.ge_u
                br_if 2 (;@4;)
                i32.const 1
                local.set 5
                local.get 6
                i32.const 1
                i32.add
                local.set 6
                local.get 0
                local.get 8
                local.get 7
                i32.load offset=16
                call_indirect (type 0)
                i32.eqz
                br_if 0 (;@6;)
                br 5 (;@1;)
              end
            end
            local.get 0
            local.get 0
            i64.load offset=8 align=4
            local.tee 12
            i32.wrap_i64
            i32.const -1612709888
            i32.and
            i32.const 536870960
            i32.or
            i32.store offset=8
            i32.const 1
            local.set 5
            local.get 0
            i32.load
            local.tee 8
            local.get 0
            i32.load offset=4
            local.tee 11
            local.get 9
            local.get 1
            local.get 2
            call 29
            br_if 3 (;@1;)
            i32.const 0
            local.set 6
            local.get 10
            local.get 7
            i32.sub
            i32.const 65535
            i32.and
            local.set 7
            loop  ;; label = @5
              local.get 6
              i32.const 65535
              i32.and
              local.get 7
              i32.ge_u
              br_if 2 (;@3;)
              i32.const 1
              local.set 5
              local.get 6
              i32.const 1
              i32.add
              local.set 6
              local.get 8
              i32.const 48
              local.get 11
              i32.load offset=16
              call_indirect (type 0)
              i32.eqz
              br_if 0 (;@5;)
              br 4 (;@1;)
            end
          end
          i32.const 1
          local.set 5
          local.get 0
          local.get 7
          local.get 9
          local.get 1
          local.get 2
          call 29
          br_if 2 (;@1;)
          local.get 0
          local.get 3
          local.get 4
          local.get 7
          i32.load offset=12
          call_indirect (type 1)
          br_if 2 (;@1;)
          local.get 10
          local.get 11
          i32.sub
          i32.const 65535
          i32.and
          local.set 11
          i32.const 0
          local.set 6
          loop  ;; label = @4
            block  ;; label = @5
              local.get 6
              i32.const 65535
              i32.and
              local.get 11
              i32.lt_u
              br_if 0 (;@5;)
              i32.const 0
              return
            end
            i32.const 1
            local.set 5
            local.get 6
            i32.const 1
            i32.add
            local.set 6
            local.get 0
            local.get 8
            local.get 7
            i32.load offset=16
            call_indirect (type 0)
            i32.eqz
            br_if 0 (;@4;)
            br 3 (;@1;)
          end
        end
        i32.const 1
        local.set 5
        local.get 8
        local.get 3
        local.get 4
        local.get 11
        i32.load offset=12
        call_indirect (type 1)
        br_if 1 (;@1;)
        local.get 0
        local.get 12
        i64.store offset=8 align=4
        i32.const 0
        return
      end
      i32.const 1
      local.set 5
      local.get 0
      i32.load
      local.tee 6
      local.get 0
      i32.load offset=4
      local.tee 0
      local.get 9
      local.get 1
      local.get 2
      call 29
      br_if 0 (;@1;)
      local.get 6
      local.get 3
      local.get 4
      local.get 0
      i32.load offset=12
      call_indirect (type 1)
      local.set 5
    end
    local.get 5)
  (func (;29;) (type 10) (param i32 i32 i32 i32 i32) (result i32)
    block  ;; label = @1
      local.get 2
      i32.const 1114112
      i32.eq
      br_if 0 (;@1;)
      local.get 0
      local.get 2
      local.get 1
      i32.load offset=16
      call_indirect (type 0)
      i32.eqz
      br_if 0 (;@1;)
      i32.const 1
      return
    end
    block  ;; label = @1
      local.get 3
      br_if 0 (;@1;)
      i32.const 0
      return
    end
    local.get 0
    local.get 3
    local.get 4
    local.get 1
    i32.load offset=12
    call_indirect (type 1))
  (func (;30;) (type 7) (param i32 i32 i32)
    (local i32 i64)
    global.get 0
    i32.const 48
    i32.sub
    local.tee 3
    global.set 0
    local.get 3
    local.get 1
    i32.store offset=4
    local.get 3
    local.get 0
    i32.store
    local.get 3
    i32.const 2
    i32.store offset=12
    local.get 3
    i32.const 8776
    i32.store offset=8
    local.get 3
    i64.const 2
    i64.store offset=20 align=4
    local.get 3
    i32.const 1
    i64.extend_i32_u
    i64.const 32
    i64.shl
    local.tee 4
    local.get 3
    i32.const 4
    i32.add
    i64.extend_i32_u
    i64.or
    i64.store offset=40
    local.get 3
    local.get 4
    local.get 3
    i64.extend_i32_u
    i64.or
    i64.store offset=32
    local.get 3
    local.get 3
    i32.const 32
    i32.add
    i32.store offset=16
    local.get 3
    i32.const 8
    i32.add
    local.get 2
    call 24
    unreachable)
  (func (;31;) (type 7) (param i32 i32 i32)
    (local i32)
    global.get 0
    i32.const 32
    i32.sub
    local.tee 3
    global.set 0
    local.get 3
    i32.const 0
    i32.store offset=16
    local.get 3
    i32.const 1
    i32.store offset=4
    local.get 3
    i64.const 4
    i64.store offset=8 align=4
    local.get 3
    local.get 1
    i32.store offset=28
    local.get 3
    local.get 0
    i32.store offset=24
    local.get 3
    local.get 3
    i32.const 24
    i32.add
    i32.store
    local.get 3
    local.get 2
    call 24
    unreachable)
  (func (;32;) (type 0) (param i32 i32) (result i32)
    (local i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32)
    local.get 0
    i32.load offset=4
    local.set 2
    local.get 0
    i32.load
    local.set 3
    block  ;; label = @1
      block  ;; label = @2
        local.get 1
        i32.load offset=8
        local.tee 4
        i32.const 402653184
        i32.and
        i32.eqz
        br_if 0 (;@2;)
        block  ;; label = @3
          block  ;; label = @4
            local.get 4
            i32.const 268435456
            i32.and
            br_if 0 (;@4;)
            block  ;; label = @5
              local.get 2
              i32.const 16
              i32.lt_u
              br_if 0 (;@5;)
              local.get 2
              local.get 3
              local.get 3
              i32.const 3
              i32.add
              i32.const -4
              i32.and
              local.tee 0
              i32.sub
              local.tee 5
              i32.add
              local.tee 6
              i32.const 3
              i32.and
              local.set 7
              i32.const 0
              local.set 8
              i32.const 0
              local.set 9
              block  ;; label = @6
                local.get 3
                local.get 0
                i32.eq
                local.tee 10
                br_if 0 (;@6;)
                i32.const 0
                local.set 9
                block  ;; label = @7
                  block  ;; label = @8
                    local.get 5
                    i32.const -4
                    i32.le_u
                    br_if 0 (;@8;)
                    i32.const 0
                    local.set 11
                    br 1 (;@7;)
                  end
                  i32.const 0
                  local.set 11
                  loop  ;; label = @8
                    local.get 9
                    local.get 3
                    local.get 11
                    i32.add
                    local.tee 12
                    i32.load8_s
                    i32.const -65
                    i32.gt_s
                    i32.add
                    local.get 12
                    i32.const 1
                    i32.add
                    i32.load8_s
                    i32.const -65
                    i32.gt_s
                    i32.add
                    local.get 12
                    i32.const 2
                    i32.add
                    i32.load8_s
                    i32.const -65
                    i32.gt_s
                    i32.add
                    local.get 12
                    i32.const 3
                    i32.add
                    i32.load8_s
                    i32.const -65
                    i32.gt_s
                    i32.add
                    local.set 9
                    local.get 11
                    i32.const 4
                    i32.add
                    local.tee 11
                    br_if 0 (;@8;)
                  end
                end
                local.get 10
                br_if 0 (;@6;)
                local.get 3
                local.get 11
                i32.add
                local.set 12
                loop  ;; label = @7
                  local.get 9
                  local.get 12
                  i32.load8_s
                  i32.const -65
                  i32.gt_s
                  i32.add
                  local.set 9
                  local.get 12
                  i32.const 1
                  i32.add
                  local.set 12
                  local.get 5
                  i32.const 1
                  i32.add
                  local.tee 5
                  br_if 0 (;@7;)
                end
              end
              block  ;; label = @6
                local.get 7
                i32.eqz
                br_if 0 (;@6;)
                local.get 0
                local.get 6
                i32.const -4
                i32.and
                i32.add
                local.tee 12
                i32.load8_s
                i32.const -65
                i32.gt_s
                local.set 8
                local.get 7
                i32.const 1
                i32.eq
                br_if 0 (;@6;)
                local.get 8
                local.get 12
                i32.load8_s offset=1
                i32.const -65
                i32.gt_s
                i32.add
                local.set 8
                local.get 7
                i32.const 2
                i32.eq
                br_if 0 (;@6;)
                local.get 8
                local.get 12
                i32.load8_s offset=2
                i32.const -65
                i32.gt_s
                i32.add
                local.set 8
              end
              local.get 6
              i32.const 2
              i32.shr_u
              local.set 5
              local.get 8
              local.get 9
              i32.add
              local.set 8
              loop  ;; label = @6
                local.get 0
                local.set 7
                local.get 5
                i32.eqz
                br_if 3 (;@3;)
                local.get 5
                i32.const 192
                local.get 5
                i32.const 192
                i32.lt_u
                select
                local.tee 6
                i32.const 3
                i32.and
                local.set 13
                local.get 6
                i32.const 2
                i32.shl
                local.set 10
                i32.const 0
                local.set 9
                block  ;; label = @7
                  local.get 5
                  i32.const 4
                  i32.lt_u
                  br_if 0 (;@7;)
                  local.get 7
                  local.get 10
                  i32.const 1008
                  i32.and
                  i32.add
                  local.set 11
                  i32.const 0
                  local.set 9
                  local.get 7
                  local.set 0
                  loop  ;; label = @8
                    local.get 0
                    i32.const 12
                    i32.add
                    i32.load
                    local.tee 12
                    i32.const -1
                    i32.xor
                    i32.const 7
                    i32.shr_u
                    local.get 12
                    i32.const 6
                    i32.shr_u
                    i32.or
                    i32.const 16843009
                    i32.and
                    local.get 0
                    i32.const 8
                    i32.add
                    i32.load
                    local.tee 12
                    i32.const -1
                    i32.xor
                    i32.const 7
                    i32.shr_u
                    local.get 12
                    i32.const 6
                    i32.shr_u
                    i32.or
                    i32.const 16843009
                    i32.and
                    local.get 0
                    i32.const 4
                    i32.add
                    i32.load
                    local.tee 12
                    i32.const -1
                    i32.xor
                    i32.const 7
                    i32.shr_u
                    local.get 12
                    i32.const 6
                    i32.shr_u
                    i32.or
                    i32.const 16843009
                    i32.and
                    local.get 0
                    i32.load
                    local.tee 12
                    i32.const -1
                    i32.xor
                    i32.const 7
                    i32.shr_u
                    local.get 12
                    i32.const 6
                    i32.shr_u
                    i32.or
                    i32.const 16843009
                    i32.and
                    local.get 9
                    i32.add
                    i32.add
                    i32.add
                    i32.add
                    local.set 9
                    local.get 0
                    i32.const 16
                    i32.add
                    local.tee 0
                    local.get 11
                    i32.ne
                    br_if 0 (;@8;)
                  end
                end
                local.get 5
                local.get 6
                i32.sub
                local.set 5
                local.get 7
                local.get 10
                i32.add
                local.set 0
                local.get 9
                i32.const 8
                i32.shr_u
                i32.const 16711935
                i32.and
                local.get 9
                i32.const 16711935
                i32.and
                i32.add
                i32.const 65537
                i32.mul
                i32.const 16
                i32.shr_u
                local.get 8
                i32.add
                local.set 8
                local.get 13
                i32.eqz
                br_if 0 (;@6;)
              end
              local.get 7
              local.get 6
              i32.const 252
              i32.and
              i32.const 2
              i32.shl
              i32.add
              local.tee 9
              i32.load
              local.tee 0
              i32.const -1
              i32.xor
              i32.const 7
              i32.shr_u
              local.get 0
              i32.const 6
              i32.shr_u
              i32.or
              i32.const 16843009
              i32.and
              local.set 0
              block  ;; label = @6
                local.get 13
                i32.const 1
                i32.eq
                br_if 0 (;@6;)
                local.get 9
                i32.load offset=4
                local.tee 12
                i32.const -1
                i32.xor
                i32.const 7
                i32.shr_u
                local.get 12
                i32.const 6
                i32.shr_u
                i32.or
                i32.const 16843009
                i32.and
                local.get 0
                i32.add
                local.set 0
                local.get 13
                i32.const 2
                i32.eq
                br_if 0 (;@6;)
                local.get 9
                i32.load offset=8
                local.tee 9
                i32.const -1
                i32.xor
                i32.const 7
                i32.shr_u
                local.get 9
                i32.const 6
                i32.shr_u
                i32.or
                i32.const 16843009
                i32.and
                local.get 0
                i32.add
                local.set 0
              end
              local.get 0
              i32.const 8
              i32.shr_u
              i32.const 459007
              i32.and
              local.get 0
              i32.const 16711935
              i32.and
              i32.add
              i32.const 65537
              i32.mul
              i32.const 16
              i32.shr_u
              local.get 8
              i32.add
              local.set 8
              br 2 (;@3;)
            end
            block  ;; label = @5
              local.get 2
              br_if 0 (;@5;)
              i32.const 0
              local.set 2
              i32.const 0
              local.set 8
              br 2 (;@3;)
            end
            local.get 2
            i32.const 3
            i32.and
            local.set 12
            block  ;; label = @5
              block  ;; label = @6
                local.get 2
                i32.const 4
                i32.ge_u
                br_if 0 (;@6;)
                i32.const 0
                local.set 8
                i32.const 0
                local.set 9
                br 1 (;@5;)
              end
              local.get 2
              i32.const 12
              i32.and
              local.set 11
              i32.const 0
              local.set 8
              i32.const 0
              local.set 9
              loop  ;; label = @6
                local.get 8
                local.get 3
                local.get 9
                i32.add
                local.tee 0
                i32.load8_s
                i32.const -65
                i32.gt_s
                i32.add
                local.get 0
                i32.const 1
                i32.add
                i32.load8_s
                i32.const -65
                i32.gt_s
                i32.add
                local.get 0
                i32.const 2
                i32.add
                i32.load8_s
                i32.const -65
                i32.gt_s
                i32.add
                local.get 0
                i32.const 3
                i32.add
                i32.load8_s
                i32.const -65
                i32.gt_s
                i32.add
                local.set 8
                local.get 11
                local.get 9
                i32.const 4
                i32.add
                local.tee 9
                i32.ne
                br_if 0 (;@6;)
              end
            end
            local.get 12
            i32.eqz
            br_if 1 (;@3;)
            local.get 3
            local.get 9
            i32.add
            local.set 0
            loop  ;; label = @5
              local.get 8
              local.get 0
              i32.load8_s
              i32.const -65
              i32.gt_s
              i32.add
              local.set 8
              local.get 0
              i32.const 1
              i32.add
              local.set 0
              local.get 12
              i32.const -1
              i32.add
              local.tee 12
              br_if 0 (;@5;)
              br 2 (;@3;)
            end
          end
          block  ;; label = @4
            block  ;; label = @5
              local.get 1
              i32.load16_u offset=14
              local.tee 11
              br_if 0 (;@5;)
              i32.const 0
              local.set 2
              i32.const 0
              local.set 0
              br 1 (;@4;)
            end
            local.get 3
            local.get 2
            i32.add
            local.set 5
            i32.const 0
            local.set 2
            i32.const 0
            local.set 12
            local.get 3
            local.set 9
            block  ;; label = @5
              loop  ;; label = @6
                local.get 9
                local.tee 0
                local.get 5
                i32.eq
                br_if 1 (;@5;)
                block  ;; label = @7
                  block  ;; label = @8
                    local.get 0
                    i32.load8_s
                    local.tee 9
                    i32.const -1
                    i32.le_s
                    br_if 0 (;@8;)
                    local.get 0
                    i32.const 1
                    i32.add
                    local.set 9
                    br 1 (;@7;)
                  end
                  block  ;; label = @8
                    local.get 9
                    i32.const -32
                    i32.ge_u
                    br_if 0 (;@8;)
                    local.get 0
                    i32.const 2
                    i32.add
                    local.set 9
                    br 1 (;@7;)
                  end
                  block  ;; label = @8
                    local.get 9
                    i32.const -16
                    i32.ge_u
                    br_if 0 (;@8;)
                    local.get 0
                    i32.const 3
                    i32.add
                    local.set 9
                    br 1 (;@7;)
                  end
                  local.get 0
                  i32.const 4
                  i32.add
                  local.set 9
                end
                local.get 9
                local.get 0
                i32.sub
                local.get 2
                i32.add
                local.set 2
                local.get 11
                local.get 12
                i32.const 1
                i32.add
                local.tee 12
                i32.ne
                br_if 0 (;@6;)
              end
              i32.const 0
              local.set 0
              br 1 (;@4;)
            end
            local.get 11
            local.get 12
            i32.sub
            local.set 0
          end
          local.get 11
          local.get 0
          i32.sub
          local.set 8
        end
        local.get 8
        local.get 1
        i32.load16_u offset=12
        local.tee 0
        i32.lt_u
        br_if 1 (;@1;)
      end
      local.get 1
      i32.load
      local.get 3
      local.get 2
      local.get 1
      i32.load offset=4
      i32.load offset=12
      call_indirect (type 1)
      return
    end
    local.get 0
    local.get 8
    i32.sub
    local.set 7
    i32.const 0
    local.set 0
    i32.const 0
    local.set 8
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          local.get 4
          i32.const 29
          i32.shr_u
          i32.const 3
          i32.and
          br_table 2 (;@1;) 0 (;@3;) 1 (;@2;) 2 (;@1;) 2 (;@1;)
        end
        local.get 7
        local.set 8
        br 1 (;@1;)
      end
      local.get 7
      i32.const 65534
      i32.and
      i32.const 1
      i32.shr_u
      local.set 8
    end
    local.get 4
    i32.const 2097151
    i32.and
    local.set 5
    local.get 1
    i32.load offset=4
    local.set 9
    local.get 1
    i32.load
    local.set 12
    block  ;; label = @1
      block  ;; label = @2
        loop  ;; label = @3
          local.get 0
          i32.const 65535
          i32.and
          local.get 8
          i32.const 65535
          i32.and
          i32.ge_u
          br_if 1 (;@2;)
          i32.const 1
          local.set 11
          local.get 0
          i32.const 1
          i32.add
          local.set 0
          local.get 12
          local.get 5
          local.get 9
          i32.load offset=16
          call_indirect (type 0)
          i32.eqz
          br_if 0 (;@3;)
          br 2 (;@1;)
        end
      end
      i32.const 1
      local.set 11
      local.get 12
      local.get 3
      local.get 2
      local.get 9
      i32.load offset=12
      call_indirect (type 1)
      br_if 0 (;@1;)
      local.get 7
      local.get 8
      i32.sub
      i32.const 65535
      i32.and
      local.set 8
      i32.const 0
      local.set 0
      loop  ;; label = @2
        block  ;; label = @3
          local.get 0
          i32.const 65535
          i32.and
          local.get 8
          i32.lt_u
          br_if 0 (;@3;)
          i32.const 0
          return
        end
        i32.const 1
        local.set 11
        local.get 0
        i32.const 1
        i32.add
        local.set 0
        local.get 12
        local.get 5
        local.get 9
        i32.load offset=16
        call_indirect (type 0)
        i32.eqz
        br_if 0 (;@2;)
      end
    end
    local.get 11)
  (func (;33;) (type 2) (param i32 i32)
    local.get 0
    i32.const 0
    i32.store)
  (func (;34;) (type 11) (param i32 i32 i32 i32)
    (local i32 i32)
    global.get 0
    i32.const 16
    i32.sub
    local.tee 4
    global.set 0
    i32.const 0
    i32.const 0
    i32.load offset=9236
    local.tee 5
    i32.const 1
    i32.add
    i32.store offset=9236
    block  ;; label = @1
      local.get 5
      i32.const 0
      i32.lt_s
      br_if 0 (;@1;)
      block  ;; label = @2
        block  ;; label = @3
          i32.const 0
          i32.load8_u offset=9696
          br_if 0 (;@3;)
          i32.const 0
          i32.const 0
          i32.load offset=9692
          i32.const 1
          i32.add
          i32.store offset=9692
          i32.const 0
          i32.load offset=9232
          i32.const -1
          i32.gt_s
          br_if 1 (;@2;)
          br 2 (;@1;)
        end
        local.get 4
        i32.const 8
        i32.add
        local.get 0
        local.get 1
        call_indirect (type 2)
        unreachable
      end
      i32.const 0
      i32.const 0
      i32.store8 offset=9696
      local.get 2
      i32.eqz
      br_if 0 (;@1;)
      call 35
      unreachable
    end
    unreachable)
  (func (;35;) (type 8)
    unreachable)
  (func (;36;) (type 4) (param i32)
    local.get 0
    call 37
    unreachable)
  (func (;37;) (type 4) (param i32)
    (local i32 i32 i32)
    global.get 0
    i32.const 16
    i32.sub
    local.tee 1
    global.set 0
    local.get 0
    i32.load
    local.tee 2
    i32.load offset=12
    local.set 3
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            local.get 2
            i32.load offset=4
            br_table 0 (;@4;) 1 (;@3;) 2 (;@2;)
          end
          local.get 3
          br_if 1 (;@2;)
          i32.const 1
          local.set 2
          i32.const 0
          local.set 3
          br 2 (;@1;)
        end
        local.get 3
        br_if 0 (;@2;)
        local.get 2
        i32.load
        local.tee 2
        i32.load offset=4
        local.set 3
        local.get 2
        i32.load
        local.set 2
        br 1 (;@1;)
      end
      local.get 1
      i32.const -2147483648
      i32.store
      local.get 1
      local.get 0
      i32.store offset=12
      local.get 1
      i32.const 3
      local.get 0
      i32.load offset=8
      local.tee 0
      i32.load8_u offset=8
      local.get 0
      i32.load8_u offset=9
      call 34
      unreachable
    end
    local.get 1
    local.get 3
    i32.store offset=4
    local.get 1
    local.get 2
    i32.store
    local.get 1
    i32.const 4
    local.get 0
    i32.load offset=8
    local.tee 0
    i32.load8_u offset=8
    local.get 0
    i32.load8_u offset=9
    call 34
    unreachable)
  (func (;38;) (type 2) (param i32 i32)
    local.get 0
    local.get 1
    i64.load align=4
    i64.store)
  (func (;39;) (type 9) (param i32) (result i32)
    (local i32 i32 i32 i32 i32 i32 i32 i32 i64)
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  block  ;; label = @8
                    local.get 0
                    i32.const 244
                    i32.gt_u
                    br_if 0 (;@8;)
                    i32.const 0
                    i32.load offset=9648
                    local.tee 1
                    i32.const 16
                    local.get 0
                    i32.const 11
                    i32.add
                    i32.const 504
                    i32.and
                    local.get 0
                    i32.const 11
                    i32.lt_u
                    select
                    local.tee 2
                    i32.const 3
                    i32.shr_u
                    local.tee 3
                    i32.shr_u
                    local.tee 0
                    i32.const 3
                    i32.and
                    br_if 1 (;@7;)
                    local.get 2
                    i32.const 0
                    i32.load offset=9656
                    i32.le_u
                    br_if 7 (;@1;)
                    local.get 0
                    br_if 2 (;@6;)
                    i32.const 0
                    i32.load offset=9652
                    local.tee 0
                    br_if 3 (;@5;)
                    br 7 (;@1;)
                  end
                  local.get 0
                  i32.const 11
                  i32.add
                  local.tee 3
                  i32.const -8
                  i32.and
                  local.set 2
                  i32.const 0
                  i32.load offset=9652
                  local.tee 4
                  i32.eqz
                  br_if 6 (;@1;)
                  i32.const 31
                  local.set 5
                  block  ;; label = @8
                    local.get 0
                    i32.const 16777204
                    i32.gt_u
                    br_if 0 (;@8;)
                    local.get 2
                    i32.const 6
                    local.get 3
                    i32.const 8
                    i32.shr_u
                    i32.clz
                    local.tee 0
                    i32.sub
                    i32.shr_u
                    i32.const 1
                    i32.and
                    local.get 0
                    i32.const 1
                    i32.shl
                    i32.sub
                    i32.const 62
                    i32.add
                    local.set 5
                  end
                  i32.const 0
                  local.get 2
                  i32.sub
                  local.set 3
                  block  ;; label = @8
                    local.get 5
                    i32.const 2
                    i32.shl
                    i32.const 9240
                    i32.add
                    i32.load
                    local.tee 1
                    br_if 0 (;@8;)
                    i32.const 0
                    local.set 0
                    i32.const 0
                    local.set 6
                    br 4 (;@4;)
                  end
                  i32.const 0
                  local.set 0
                  local.get 2
                  i32.const 0
                  i32.const 25
                  local.get 5
                  i32.const 1
                  i32.shr_u
                  i32.sub
                  local.get 5
                  i32.const 31
                  i32.eq
                  select
                  i32.shl
                  local.set 7
                  i32.const 0
                  local.set 6
                  loop  ;; label = @8
                    block  ;; label = @9
                      local.get 1
                      local.tee 1
                      i32.load offset=4
                      i32.const -8
                      i32.and
                      local.tee 8
                      local.get 2
                      i32.lt_u
                      br_if 0 (;@9;)
                      local.get 8
                      local.get 2
                      i32.sub
                      local.tee 8
                      local.get 3
                      i32.ge_u
                      br_if 0 (;@9;)
                      local.get 8
                      local.set 3
                      local.get 1
                      local.set 6
                      local.get 8
                      br_if 0 (;@9;)
                      i32.const 0
                      local.set 3
                      local.get 1
                      local.set 6
                      local.get 1
                      local.set 0
                      br 6 (;@3;)
                    end
                    local.get 1
                    i32.load offset=20
                    local.tee 8
                    local.get 0
                    local.get 8
                    local.get 1
                    local.get 7
                    i32.const 29
                    i32.shr_u
                    i32.const 4
                    i32.and
                    i32.add
                    i32.load offset=16
                    local.tee 1
                    i32.ne
                    select
                    local.get 0
                    local.get 8
                    select
                    local.set 0
                    local.get 7
                    i32.const 1
                    i32.shl
                    local.set 7
                    local.get 1
                    i32.eqz
                    br_if 4 (;@4;)
                    br 0 (;@8;)
                  end
                end
                block  ;; label = @7
                  block  ;; label = @8
                    local.get 0
                    i32.const -1
                    i32.xor
                    i32.const 1
                    i32.and
                    local.get 3
                    i32.add
                    local.tee 7
                    i32.const 3
                    i32.shl
                    local.tee 0
                    i32.const 9384
                    i32.add
                    local.tee 2
                    local.get 0
                    i32.const 9392
                    i32.add
                    i32.load
                    local.tee 3
                    i32.load offset=8
                    local.tee 6
                    i32.eq
                    br_if 0 (;@8;)
                    local.get 6
                    local.get 2
                    i32.store offset=12
                    local.get 2
                    local.get 6
                    i32.store offset=8
                    br 1 (;@7;)
                  end
                  i32.const 0
                  local.get 1
                  i32.const -2
                  local.get 7
                  i32.rotl
                  i32.and
                  i32.store offset=9648
                end
                local.get 3
                local.get 0
                i32.const 3
                i32.or
                i32.store offset=4
                local.get 3
                local.get 0
                i32.add
                local.tee 0
                local.get 0
                i32.load offset=4
                i32.const 1
                i32.or
                i32.store offset=4
                local.get 3
                i32.const 8
                i32.add
                return
              end
              block  ;; label = @6
                block  ;; label = @7
                  local.get 0
                  local.get 3
                  i32.shl
                  i32.const 2
                  local.get 3
                  i32.shl
                  local.tee 0
                  i32.const 0
                  local.get 0
                  i32.sub
                  i32.or
                  i32.and
                  i32.ctz
                  local.tee 8
                  i32.const 3
                  i32.shl
                  local.tee 3
                  i32.const 9384
                  i32.add
                  local.tee 6
                  local.get 3
                  i32.const 9392
                  i32.add
                  i32.load
                  local.tee 0
                  i32.load offset=8
                  local.tee 7
                  i32.eq
                  br_if 0 (;@7;)
                  local.get 7
                  local.get 6
                  i32.store offset=12
                  local.get 6
                  local.get 7
                  i32.store offset=8
                  br 1 (;@6;)
                end
                i32.const 0
                local.get 1
                i32.const -2
                local.get 8
                i32.rotl
                i32.and
                i32.store offset=9648
              end
              local.get 0
              local.get 2
              i32.const 3
              i32.or
              i32.store offset=4
              local.get 0
              local.get 2
              i32.add
              local.tee 7
              local.get 3
              local.get 2
              i32.sub
              local.tee 2
              i32.const 1
              i32.or
              i32.store offset=4
              local.get 0
              local.get 3
              i32.add
              local.get 2
              i32.store
              block  ;; label = @6
                i32.const 0
                i32.load offset=9656
                local.tee 1
                i32.eqz
                br_if 0 (;@6;)
                local.get 1
                i32.const -8
                i32.and
                i32.const 9384
                i32.add
                local.set 6
                i32.const 0
                i32.load offset=9664
                local.set 3
                block  ;; label = @7
                  block  ;; label = @8
                    i32.const 0
                    i32.load offset=9648
                    local.tee 8
                    i32.const 1
                    local.get 1
                    i32.const 3
                    i32.shr_u
                    i32.shl
                    local.tee 1
                    i32.and
                    br_if 0 (;@8;)
                    i32.const 0
                    local.get 8
                    local.get 1
                    i32.or
                    i32.store offset=9648
                    local.get 6
                    local.set 1
                    br 1 (;@7;)
                  end
                  local.get 6
                  i32.load offset=8
                  local.set 1
                end
                local.get 6
                local.get 3
                i32.store offset=8
                local.get 1
                local.get 3
                i32.store offset=12
                local.get 3
                local.get 6
                i32.store offset=12
                local.get 3
                local.get 1
                i32.store offset=8
              end
              i32.const 0
              local.get 7
              i32.store offset=9664
              i32.const 0
              local.get 2
              i32.store offset=9656
              local.get 0
              i32.const 8
              i32.add
              return
            end
            local.get 0
            i32.ctz
            i32.const 2
            i32.shl
            i32.const 9240
            i32.add
            i32.load
            local.tee 6
            i32.load offset=4
            i32.const -8
            i32.and
            local.get 2
            i32.sub
            local.set 3
            local.get 6
            local.set 1
            block  ;; label = @5
              block  ;; label = @6
                loop  ;; label = @7
                  block  ;; label = @8
                    local.get 6
                    i32.load offset=16
                    local.tee 0
                    br_if 0 (;@8;)
                    local.get 6
                    i32.load offset=20
                    local.tee 0
                    br_if 0 (;@8;)
                    local.get 1
                    i32.load offset=24
                    local.set 5
                    block  ;; label = @9
                      block  ;; label = @10
                        block  ;; label = @11
                          local.get 1
                          i32.load offset=12
                          local.tee 0
                          local.get 1
                          i32.ne
                          br_if 0 (;@11;)
                          local.get 1
                          i32.const 20
                          i32.const 16
                          local.get 1
                          i32.load offset=20
                          local.tee 0
                          select
                          i32.add
                          i32.load
                          local.tee 6
                          br_if 1 (;@10;)
                          i32.const 0
                          local.set 0
                          br 2 (;@9;)
                        end
                        local.get 1
                        i32.load offset=8
                        local.tee 6
                        local.get 0
                        i32.store offset=12
                        local.get 0
                        local.get 6
                        i32.store offset=8
                        br 1 (;@9;)
                      end
                      local.get 1
                      i32.const 20
                      i32.add
                      local.get 1
                      i32.const 16
                      i32.add
                      local.get 0
                      select
                      local.set 7
                      loop  ;; label = @10
                        local.get 7
                        local.set 8
                        local.get 6
                        local.tee 0
                        i32.const 20
                        i32.add
                        local.get 0
                        i32.const 16
                        i32.add
                        local.get 0
                        i32.load offset=20
                        local.tee 6
                        select
                        local.set 7
                        local.get 0
                        i32.const 20
                        i32.const 16
                        local.get 6
                        select
                        i32.add
                        i32.load
                        local.tee 6
                        br_if 0 (;@10;)
                      end
                      local.get 8
                      i32.const 0
                      i32.store
                    end
                    local.get 5
                    i32.eqz
                    br_if 3 (;@5;)
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 1
                        local.get 1
                        i32.load offset=28
                        i32.const 2
                        i32.shl
                        i32.const 9240
                        i32.add
                        local.tee 6
                        i32.load
                        i32.eq
                        br_if 0 (;@10;)
                        block  ;; label = @11
                          local.get 5
                          i32.load offset=16
                          local.get 1
                          i32.eq
                          br_if 0 (;@11;)
                          local.get 5
                          local.get 0
                          i32.store offset=20
                          local.get 0
                          br_if 2 (;@9;)
                          br 6 (;@5;)
                        end
                        local.get 5
                        local.get 0
                        i32.store offset=16
                        local.get 0
                        br_if 1 (;@9;)
                        br 5 (;@5;)
                      end
                      local.get 6
                      local.get 0
                      i32.store
                      local.get 0
                      i32.eqz
                      br_if 3 (;@6;)
                    end
                    local.get 0
                    local.get 5
                    i32.store offset=24
                    block  ;; label = @9
                      local.get 1
                      i32.load offset=16
                      local.tee 6
                      i32.eqz
                      br_if 0 (;@9;)
                      local.get 0
                      local.get 6
                      i32.store offset=16
                      local.get 6
                      local.get 0
                      i32.store offset=24
                    end
                    local.get 1
                    i32.load offset=20
                    local.tee 6
                    i32.eqz
                    br_if 3 (;@5;)
                    local.get 0
                    local.get 6
                    i32.store offset=20
                    local.get 6
                    local.get 0
                    i32.store offset=24
                    br 3 (;@5;)
                  end
                  local.get 0
                  i32.load offset=4
                  i32.const -8
                  i32.and
                  local.get 2
                  i32.sub
                  local.tee 6
                  local.get 3
                  local.get 6
                  local.get 3
                  i32.lt_u
                  local.tee 6
                  select
                  local.set 3
                  local.get 0
                  local.get 1
                  local.get 6
                  select
                  local.set 1
                  local.get 0
                  local.set 6
                  br 0 (;@7;)
                end
              end
              i32.const 0
              i32.const 0
              i32.load offset=9652
              i32.const -2
              local.get 1
              i32.load offset=28
              i32.rotl
              i32.and
              i32.store offset=9652
            end
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  local.get 3
                  i32.const 16
                  i32.lt_u
                  br_if 0 (;@7;)
                  local.get 1
                  local.get 2
                  i32.const 3
                  i32.or
                  i32.store offset=4
                  local.get 1
                  local.get 2
                  i32.add
                  local.tee 2
                  local.get 3
                  i32.const 1
                  i32.or
                  i32.store offset=4
                  local.get 2
                  local.get 3
                  i32.add
                  local.get 3
                  i32.store
                  i32.const 0
                  i32.load offset=9656
                  local.tee 7
                  i32.eqz
                  br_if 1 (;@6;)
                  local.get 7
                  i32.const -8
                  i32.and
                  i32.const 9384
                  i32.add
                  local.set 6
                  i32.const 0
                  i32.load offset=9664
                  local.set 0
                  block  ;; label = @8
                    block  ;; label = @9
                      i32.const 0
                      i32.load offset=9648
                      local.tee 8
                      i32.const 1
                      local.get 7
                      i32.const 3
                      i32.shr_u
                      i32.shl
                      local.tee 7
                      i32.and
                      br_if 0 (;@9;)
                      i32.const 0
                      local.get 8
                      local.get 7
                      i32.or
                      i32.store offset=9648
                      local.get 6
                      local.set 7
                      br 1 (;@8;)
                    end
                    local.get 6
                    i32.load offset=8
                    local.set 7
                  end
                  local.get 6
                  local.get 0
                  i32.store offset=8
                  local.get 7
                  local.get 0
                  i32.store offset=12
                  local.get 0
                  local.get 6
                  i32.store offset=12
                  local.get 0
                  local.get 7
                  i32.store offset=8
                  br 1 (;@6;)
                end
                local.get 1
                local.get 3
                local.get 2
                i32.add
                local.tee 0
                i32.const 3
                i32.or
                i32.store offset=4
                local.get 1
                local.get 0
                i32.add
                local.tee 0
                local.get 0
                i32.load offset=4
                i32.const 1
                i32.or
                i32.store offset=4
                br 1 (;@5;)
              end
              i32.const 0
              local.get 2
              i32.store offset=9664
              i32.const 0
              local.get 3
              i32.store offset=9656
            end
            local.get 1
            i32.const 8
            i32.add
            return
          end
          block  ;; label = @4
            local.get 0
            local.get 6
            i32.or
            br_if 0 (;@4;)
            i32.const 0
            local.set 6
            i32.const 2
            local.get 5
            i32.shl
            local.tee 0
            i32.const 0
            local.get 0
            i32.sub
            i32.or
            local.get 4
            i32.and
            local.tee 0
            i32.eqz
            br_if 3 (;@1;)
            local.get 0
            i32.ctz
            i32.const 2
            i32.shl
            i32.const 9240
            i32.add
            i32.load
            local.set 0
          end
          local.get 0
          i32.eqz
          br_if 1 (;@2;)
        end
        loop  ;; label = @3
          local.get 0
          local.get 6
          local.get 0
          i32.load offset=4
          i32.const -8
          i32.and
          local.tee 1
          local.get 2
          i32.sub
          local.tee 8
          local.get 3
          i32.lt_u
          local.tee 5
          select
          local.set 4
          local.get 1
          local.get 2
          i32.lt_u
          local.set 7
          local.get 8
          local.get 3
          local.get 5
          select
          local.set 8
          block  ;; label = @4
            local.get 0
            i32.load offset=16
            local.tee 1
            br_if 0 (;@4;)
            local.get 0
            i32.load offset=20
            local.set 1
          end
          local.get 6
          local.get 4
          local.get 7
          select
          local.set 6
          local.get 3
          local.get 8
          local.get 7
          select
          local.set 3
          local.get 1
          local.set 0
          local.get 1
          br_if 0 (;@3;)
        end
      end
      local.get 6
      i32.eqz
      br_if 0 (;@1;)
      block  ;; label = @2
        i32.const 0
        i32.load offset=9656
        local.tee 0
        local.get 2
        i32.lt_u
        br_if 0 (;@2;)
        local.get 3
        local.get 0
        local.get 2
        i32.sub
        i32.ge_u
        br_if 1 (;@1;)
      end
      local.get 6
      i32.load offset=24
      local.set 5
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            local.get 6
            i32.load offset=12
            local.tee 0
            local.get 6
            i32.ne
            br_if 0 (;@4;)
            local.get 6
            i32.const 20
            i32.const 16
            local.get 6
            i32.load offset=20
            local.tee 0
            select
            i32.add
            i32.load
            local.tee 1
            br_if 1 (;@3;)
            i32.const 0
            local.set 0
            br 2 (;@2;)
          end
          local.get 6
          i32.load offset=8
          local.tee 1
          local.get 0
          i32.store offset=12
          local.get 0
          local.get 1
          i32.store offset=8
          br 1 (;@2;)
        end
        local.get 6
        i32.const 20
        i32.add
        local.get 6
        i32.const 16
        i32.add
        local.get 0
        select
        local.set 7
        loop  ;; label = @3
          local.get 7
          local.set 8
          local.get 1
          local.tee 0
          i32.const 20
          i32.add
          local.get 0
          i32.const 16
          i32.add
          local.get 0
          i32.load offset=20
          local.tee 1
          select
          local.set 7
          local.get 0
          i32.const 20
          i32.const 16
          local.get 1
          select
          i32.add
          i32.load
          local.tee 1
          br_if 0 (;@3;)
        end
        local.get 8
        i32.const 0
        i32.store
      end
      block  ;; label = @2
        local.get 5
        i32.eqz
        br_if 0 (;@2;)
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              local.get 6
              local.get 6
              i32.load offset=28
              i32.const 2
              i32.shl
              i32.const 9240
              i32.add
              local.tee 1
              i32.load
              i32.eq
              br_if 0 (;@5;)
              block  ;; label = @6
                local.get 5
                i32.load offset=16
                local.get 6
                i32.eq
                br_if 0 (;@6;)
                local.get 5
                local.get 0
                i32.store offset=20
                local.get 0
                br_if 2 (;@4;)
                br 4 (;@2;)
              end
              local.get 5
              local.get 0
              i32.store offset=16
              local.get 0
              br_if 1 (;@4;)
              br 3 (;@2;)
            end
            local.get 1
            local.get 0
            i32.store
            local.get 0
            i32.eqz
            br_if 1 (;@3;)
          end
          local.get 0
          local.get 5
          i32.store offset=24
          block  ;; label = @4
            local.get 6
            i32.load offset=16
            local.tee 1
            i32.eqz
            br_if 0 (;@4;)
            local.get 0
            local.get 1
            i32.store offset=16
            local.get 1
            local.get 0
            i32.store offset=24
          end
          local.get 6
          i32.load offset=20
          local.tee 1
          i32.eqz
          br_if 1 (;@2;)
          local.get 0
          local.get 1
          i32.store offset=20
          local.get 1
          local.get 0
          i32.store offset=24
          br 1 (;@2;)
        end
        i32.const 0
        i32.const 0
        i32.load offset=9652
        i32.const -2
        local.get 6
        i32.load offset=28
        i32.rotl
        i32.and
        i32.store offset=9652
      end
      block  ;; label = @2
        block  ;; label = @3
          local.get 3
          i32.const 16
          i32.lt_u
          br_if 0 (;@3;)
          local.get 6
          local.get 2
          i32.const 3
          i32.or
          i32.store offset=4
          local.get 6
          local.get 2
          i32.add
          local.tee 2
          local.get 3
          i32.const 1
          i32.or
          i32.store offset=4
          local.get 2
          local.get 3
          i32.add
          local.get 3
          i32.store
          block  ;; label = @4
            local.get 3
            i32.const 256
            i32.lt_u
            br_if 0 (;@4;)
            i32.const 31
            local.set 0
            block  ;; label = @5
              local.get 3
              i32.const 16777215
              i32.gt_u
              br_if 0 (;@5;)
              local.get 3
              i32.const 6
              local.get 3
              i32.const 8
              i32.shr_u
              i32.clz
              local.tee 0
              i32.sub
              i32.shr_u
              i32.const 1
              i32.and
              local.get 0
              i32.const 1
              i32.shl
              i32.sub
              i32.const 62
              i32.add
              local.set 0
            end
            local.get 2
            i64.const 0
            i64.store offset=16 align=4
            local.get 2
            local.get 0
            i32.store offset=28
            local.get 0
            i32.const 2
            i32.shl
            i32.const 9240
            i32.add
            local.set 1
            block  ;; label = @5
              i32.const 0
              i32.load offset=9652
              i32.const 1
              local.get 0
              i32.shl
              local.tee 7
              i32.and
              br_if 0 (;@5;)
              local.get 1
              local.get 2
              i32.store
              local.get 2
              local.get 1
              i32.store offset=24
              local.get 2
              local.get 2
              i32.store offset=12
              local.get 2
              local.get 2
              i32.store offset=8
              i32.const 0
              i32.const 0
              i32.load offset=9652
              local.get 7
              i32.or
              i32.store offset=9652
              br 3 (;@2;)
            end
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  local.get 1
                  i32.load
                  local.tee 7
                  i32.load offset=4
                  i32.const -8
                  i32.and
                  local.get 3
                  i32.ne
                  br_if 0 (;@7;)
                  local.get 7
                  local.set 0
                  br 1 (;@6;)
                end
                local.get 3
                i32.const 0
                i32.const 25
                local.get 0
                i32.const 1
                i32.shr_u
                i32.sub
                local.get 0
                i32.const 31
                i32.eq
                select
                i32.shl
                local.set 1
                loop  ;; label = @7
                  local.get 7
                  local.get 1
                  i32.const 29
                  i32.shr_u
                  i32.const 4
                  i32.and
                  i32.add
                  local.tee 8
                  i32.load offset=16
                  local.tee 0
                  i32.eqz
                  br_if 2 (;@5;)
                  local.get 1
                  i32.const 1
                  i32.shl
                  local.set 1
                  local.get 0
                  local.set 7
                  local.get 0
                  i32.load offset=4
                  i32.const -8
                  i32.and
                  local.get 3
                  i32.ne
                  br_if 0 (;@7;)
                end
              end
              local.get 0
              i32.load offset=8
              local.tee 3
              local.get 2
              i32.store offset=12
              local.get 0
              local.get 2
              i32.store offset=8
              local.get 2
              i32.const 0
              i32.store offset=24
              local.get 2
              local.get 0
              i32.store offset=12
              local.get 2
              local.get 3
              i32.store offset=8
              br 3 (;@2;)
            end
            local.get 8
            i32.const 16
            i32.add
            local.get 2
            i32.store
            local.get 2
            local.get 7
            i32.store offset=24
            local.get 2
            local.get 2
            i32.store offset=12
            local.get 2
            local.get 2
            i32.store offset=8
            br 2 (;@2;)
          end
          local.get 3
          i32.const 248
          i32.and
          i32.const 9384
          i32.add
          local.set 0
          block  ;; label = @4
            block  ;; label = @5
              i32.const 0
              i32.load offset=9648
              local.tee 1
              i32.const 1
              local.get 3
              i32.const 3
              i32.shr_u
              i32.shl
              local.tee 3
              i32.and
              br_if 0 (;@5;)
              i32.const 0
              local.get 1
              local.get 3
              i32.or
              i32.store offset=9648
              local.get 0
              local.set 3
              br 1 (;@4;)
            end
            local.get 0
            i32.load offset=8
            local.set 3
          end
          local.get 0
          local.get 2
          i32.store offset=8
          local.get 3
          local.get 2
          i32.store offset=12
          local.get 2
          local.get 0
          i32.store offset=12
          local.get 2
          local.get 3
          i32.store offset=8
          br 1 (;@2;)
        end
        local.get 6
        local.get 3
        local.get 2
        i32.add
        local.tee 0
        i32.const 3
        i32.or
        i32.store offset=4
        local.get 6
        local.get 0
        i32.add
        local.tee 0
        local.get 0
        i32.load offset=4
        i32.const 1
        i32.or
        i32.store offset=4
      end
      local.get 6
      i32.const 8
      i32.add
      return
    end
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  i32.const 0
                  i32.load offset=9656
                  local.tee 0
                  local.get 2
                  i32.ge_u
                  br_if 0 (;@7;)
                  block  ;; label = @8
                    i32.const 0
                    i32.load offset=9660
                    local.tee 0
                    local.get 2
                    i32.gt_u
                    br_if 0 (;@8;)
                    i32.const 0
                    local.set 0
                    local.get 2
                    i32.const 65583
                    i32.add
                    local.tee 6
                    i32.const 16
                    i32.shr_u
                    memory.grow
                    local.tee 3
                    i32.const -1
                    i32.eq
                    local.tee 7
                    br_if 7 (;@1;)
                    local.get 3
                    i32.const 16
                    i32.shl
                    local.tee 1
                    i32.eqz
                    br_if 7 (;@1;)
                    i32.const 0
                    i32.const 0
                    i32.load offset=9672
                    i32.const 0
                    local.get 6
                    i32.const -65536
                    i32.and
                    local.get 7
                    select
                    local.tee 8
                    i32.add
                    local.tee 0
                    i32.store offset=9672
                    i32.const 0
                    local.get 0
                    i32.const 0
                    i32.load offset=9676
                    local.tee 3
                    local.get 0
                    local.get 3
                    i32.gt_u
                    select
                    i32.store offset=9676
                    block  ;; label = @9
                      block  ;; label = @10
                        block  ;; label = @11
                          i32.const 0
                          i32.load offset=9668
                          local.tee 3
                          i32.eqz
                          br_if 0 (;@11;)
                          i32.const 9368
                          local.set 0
                          loop  ;; label = @12
                            local.get 0
                            i32.load
                            local.tee 6
                            local.get 0
                            i32.load offset=4
                            local.tee 7
                            i32.add
                            local.get 1
                            i32.eq
                            br_if 2 (;@10;)
                            local.get 0
                            i32.load offset=8
                            local.tee 0
                            br_if 0 (;@12;)
                            br 3 (;@9;)
                          end
                        end
                        block  ;; label = @11
                          block  ;; label = @12
                            i32.const 0
                            i32.load offset=9684
                            local.tee 0
                            i32.eqz
                            br_if 0 (;@12;)
                            local.get 0
                            local.get 1
                            i32.le_u
                            br_if 1 (;@11;)
                          end
                          i32.const 0
                          local.get 1
                          i32.store offset=9684
                        end
                        i32.const 0
                        i32.const 4095
                        i32.store offset=9688
                        i32.const 0
                        local.get 8
                        i32.store offset=9372
                        i32.const 0
                        local.get 1
                        i32.store offset=9368
                        i32.const 0
                        i32.const 9384
                        i32.store offset=9396
                        i32.const 0
                        i32.const 9392
                        i32.store offset=9404
                        i32.const 0
                        i32.const 9384
                        i32.store offset=9392
                        i32.const 0
                        i32.const 9400
                        i32.store offset=9412
                        i32.const 0
                        i32.const 9392
                        i32.store offset=9400
                        i32.const 0
                        i32.const 9408
                        i32.store offset=9420
                        i32.const 0
                        i32.const 9400
                        i32.store offset=9408
                        i32.const 0
                        i32.const 9416
                        i32.store offset=9428
                        i32.const 0
                        i32.const 9408
                        i32.store offset=9416
                        i32.const 0
                        i32.const 9424
                        i32.store offset=9436
                        i32.const 0
                        i32.const 9416
                        i32.store offset=9424
                        i32.const 0
                        i32.const 9432
                        i32.store offset=9444
                        i32.const 0
                        i32.const 9424
                        i32.store offset=9432
                        i32.const 0
                        i32.const 9440
                        i32.store offset=9452
                        i32.const 0
                        i32.const 9432
                        i32.store offset=9440
                        i32.const 0
                        i32.const 0
                        i32.store offset=9380
                        i32.const 0
                        i32.const 9448
                        i32.store offset=9460
                        i32.const 0
                        i32.const 9440
                        i32.store offset=9448
                        i32.const 0
                        i32.const 9448
                        i32.store offset=9456
                        i32.const 0
                        i32.const 9456
                        i32.store offset=9468
                        i32.const 0
                        i32.const 9456
                        i32.store offset=9464
                        i32.const 0
                        i32.const 9464
                        i32.store offset=9476
                        i32.const 0
                        i32.const 9464
                        i32.store offset=9472
                        i32.const 0
                        i32.const 9472
                        i32.store offset=9484
                        i32.const 0
                        i32.const 9472
                        i32.store offset=9480
                        i32.const 0
                        i32.const 9480
                        i32.store offset=9492
                        i32.const 0
                        i32.const 9480
                        i32.store offset=9488
                        i32.const 0
                        i32.const 9488
                        i32.store offset=9500
                        i32.const 0
                        i32.const 9488
                        i32.store offset=9496
                        i32.const 0
                        i32.const 9496
                        i32.store offset=9508
                        i32.const 0
                        i32.const 9496
                        i32.store offset=9504
                        i32.const 0
                        i32.const 9504
                        i32.store offset=9516
                        i32.const 0
                        i32.const 9504
                        i32.store offset=9512
                        i32.const 0
                        i32.const 9512
                        i32.store offset=9524
                        i32.const 0
                        i32.const 9520
                        i32.store offset=9532
                        i32.const 0
                        i32.const 9512
                        i32.store offset=9520
                        i32.const 0
                        i32.const 9528
                        i32.store offset=9540
                        i32.const 0
                        i32.const 9520
                        i32.store offset=9528
                        i32.const 0
                        i32.const 9536
                        i32.store offset=9548
                        i32.const 0
                        i32.const 9528
                        i32.store offset=9536
                        i32.const 0
                        i32.const 9544
                        i32.store offset=9556
                        i32.const 0
                        i32.const 9536
                        i32.store offset=9544
                        i32.const 0
                        i32.const 9552
                        i32.store offset=9564
                        i32.const 0
                        i32.const 9544
                        i32.store offset=9552
                        i32.const 0
                        i32.const 9560
                        i32.store offset=9572
                        i32.const 0
                        i32.const 9552
                        i32.store offset=9560
                        i32.const 0
                        i32.const 9568
                        i32.store offset=9580
                        i32.const 0
                        i32.const 9560
                        i32.store offset=9568
                        i32.const 0
                        i32.const 9576
                        i32.store offset=9588
                        i32.const 0
                        i32.const 9568
                        i32.store offset=9576
                        i32.const 0
                        i32.const 9584
                        i32.store offset=9596
                        i32.const 0
                        i32.const 9576
                        i32.store offset=9584
                        i32.const 0
                        i32.const 9592
                        i32.store offset=9604
                        i32.const 0
                        i32.const 9584
                        i32.store offset=9592
                        i32.const 0
                        i32.const 9600
                        i32.store offset=9612
                        i32.const 0
                        i32.const 9592
                        i32.store offset=9600
                        i32.const 0
                        i32.const 9608
                        i32.store offset=9620
                        i32.const 0
                        i32.const 9600
                        i32.store offset=9608
                        i32.const 0
                        i32.const 9616
                        i32.store offset=9628
                        i32.const 0
                        i32.const 9608
                        i32.store offset=9616
                        i32.const 0
                        i32.const 9624
                        i32.store offset=9636
                        i32.const 0
                        i32.const 9616
                        i32.store offset=9624
                        i32.const 0
                        i32.const 9632
                        i32.store offset=9644
                        i32.const 0
                        i32.const 9624
                        i32.store offset=9632
                        i32.const 0
                        local.get 1
                        i32.store offset=9668
                        i32.const 0
                        i32.const 9632
                        i32.store offset=9640
                        i32.const 0
                        local.get 8
                        i32.const -40
                        i32.add
                        local.tee 0
                        i32.store offset=9660
                        local.get 1
                        local.get 0
                        i32.const 1
                        i32.or
                        i32.store offset=4
                        local.get 1
                        local.get 0
                        i32.add
                        i32.const 40
                        i32.store offset=4
                        i32.const 0
                        i32.const 2097152
                        i32.store offset=9680
                        br 8 (;@2;)
                      end
                      local.get 3
                      local.get 1
                      i32.ge_u
                      br_if 0 (;@9;)
                      local.get 6
                      local.get 3
                      i32.gt_u
                      br_if 0 (;@9;)
                      local.get 0
                      i32.load offset=12
                      i32.eqz
                      br_if 3 (;@6;)
                    end
                    i32.const 0
                    i32.const 0
                    i32.load offset=9684
                    local.tee 0
                    local.get 1
                    local.get 0
                    local.get 1
                    i32.lt_u
                    select
                    i32.store offset=9684
                    local.get 1
                    local.get 8
                    i32.add
                    local.set 6
                    i32.const 9368
                    local.set 0
                    block  ;; label = @9
                      block  ;; label = @10
                        block  ;; label = @11
                          loop  ;; label = @12
                            local.get 0
                            i32.load
                            local.tee 7
                            local.get 6
                            i32.eq
                            br_if 1 (;@11;)
                            local.get 0
                            i32.load offset=8
                            local.tee 0
                            br_if 0 (;@12;)
                            br 2 (;@10;)
                          end
                        end
                        local.get 0
                        i32.load offset=12
                        i32.eqz
                        br_if 1 (;@9;)
                      end
                      i32.const 9368
                      local.set 0
                      block  ;; label = @10
                        loop  ;; label = @11
                          block  ;; label = @12
                            local.get 0
                            i32.load
                            local.tee 6
                            local.get 3
                            i32.gt_u
                            br_if 0 (;@12;)
                            local.get 3
                            local.get 6
                            local.get 0
                            i32.load offset=4
                            i32.add
                            local.tee 6
                            i32.lt_u
                            br_if 2 (;@10;)
                          end
                          local.get 0
                          i32.load offset=8
                          local.set 0
                          br 0 (;@11;)
                        end
                      end
                      i32.const 0
                      local.get 1
                      i32.store offset=9668
                      i32.const 0
                      local.get 8
                      i32.const -40
                      i32.add
                      local.tee 0
                      i32.store offset=9660
                      local.get 1
                      local.get 0
                      i32.const 1
                      i32.or
                      i32.store offset=4
                      local.get 1
                      local.get 0
                      i32.add
                      i32.const 40
                      i32.store offset=4
                      i32.const 0
                      i32.const 2097152
                      i32.store offset=9680
                      local.get 3
                      local.get 6
                      i32.const -32
                      i32.add
                      i32.const -8
                      i32.and
                      i32.const -8
                      i32.add
                      local.tee 0
                      local.get 0
                      local.get 3
                      i32.const 16
                      i32.add
                      i32.lt_u
                      select
                      local.tee 7
                      i32.const 27
                      i32.store offset=4
                      i32.const 0
                      i64.load offset=9368 align=4
                      local.set 9
                      local.get 7
                      i32.const 16
                      i32.add
                      i32.const 0
                      i64.load offset=9376 align=4
                      i64.store align=4
                      local.get 7
                      local.get 9
                      i64.store offset=8 align=4
                      i32.const 0
                      local.get 8
                      i32.store offset=9372
                      i32.const 0
                      local.get 1
                      i32.store offset=9368
                      i32.const 0
                      local.get 7
                      i32.const 8
                      i32.add
                      i32.store offset=9376
                      i32.const 0
                      i32.const 0
                      i32.store offset=9380
                      local.get 7
                      i32.const 28
                      i32.add
                      local.set 0
                      loop  ;; label = @10
                        local.get 0
                        i32.const 7
                        i32.store
                        local.get 0
                        i32.const 4
                        i32.add
                        local.tee 0
                        local.get 6
                        i32.lt_u
                        br_if 0 (;@10;)
                      end
                      local.get 7
                      local.get 3
                      i32.eq
                      br_if 7 (;@2;)
                      local.get 7
                      local.get 7
                      i32.load offset=4
                      i32.const -2
                      i32.and
                      i32.store offset=4
                      local.get 3
                      local.get 7
                      local.get 3
                      i32.sub
                      local.tee 0
                      i32.const 1
                      i32.or
                      i32.store offset=4
                      local.get 7
                      local.get 0
                      i32.store
                      block  ;; label = @10
                        local.get 0
                        i32.const 256
                        i32.lt_u
                        br_if 0 (;@10;)
                        local.get 3
                        local.get 0
                        call 40
                        br 8 (;@2;)
                      end
                      local.get 0
                      i32.const 248
                      i32.and
                      i32.const 9384
                      i32.add
                      local.set 6
                      block  ;; label = @10
                        block  ;; label = @11
                          i32.const 0
                          i32.load offset=9648
                          local.tee 1
                          i32.const 1
                          local.get 0
                          i32.const 3
                          i32.shr_u
                          i32.shl
                          local.tee 0
                          i32.and
                          br_if 0 (;@11;)
                          i32.const 0
                          local.get 1
                          local.get 0
                          i32.or
                          i32.store offset=9648
                          local.get 6
                          local.set 0
                          br 1 (;@10;)
                        end
                        local.get 6
                        i32.load offset=8
                        local.set 0
                      end
                      local.get 6
                      local.get 3
                      i32.store offset=8
                      local.get 0
                      local.get 3
                      i32.store offset=12
                      local.get 3
                      local.get 6
                      i32.store offset=12
                      local.get 3
                      local.get 0
                      i32.store offset=8
                      br 7 (;@2;)
                    end
                    local.get 0
                    local.get 1
                    i32.store
                    local.get 0
                    local.get 0
                    i32.load offset=4
                    local.get 8
                    i32.add
                    i32.store offset=4
                    local.get 1
                    local.get 2
                    i32.const 3
                    i32.or
                    i32.store offset=4
                    local.get 7
                    i32.const 15
                    i32.add
                    i32.const -8
                    i32.and
                    i32.const -8
                    i32.add
                    local.tee 6
                    local.get 1
                    local.get 2
                    i32.add
                    local.tee 0
                    i32.sub
                    local.set 3
                    local.get 6
                    i32.const 0
                    i32.load offset=9668
                    i32.eq
                    br_if 3 (;@5;)
                    local.get 6
                    i32.const 0
                    i32.load offset=9664
                    i32.eq
                    br_if 4 (;@4;)
                    block  ;; label = @9
                      local.get 6
                      i32.load offset=4
                      local.tee 2
                      i32.const 3
                      i32.and
                      i32.const 1
                      i32.ne
                      br_if 0 (;@9;)
                      local.get 6
                      local.get 2
                      i32.const -8
                      i32.and
                      local.tee 2
                      call 41
                      local.get 2
                      local.get 3
                      i32.add
                      local.set 3
                      local.get 6
                      local.get 2
                      i32.add
                      local.tee 6
                      i32.load offset=4
                      local.set 2
                    end
                    local.get 6
                    local.get 2
                    i32.const -2
                    i32.and
                    i32.store offset=4
                    local.get 0
                    local.get 3
                    i32.const 1
                    i32.or
                    i32.store offset=4
                    local.get 0
                    local.get 3
                    i32.add
                    local.get 3
                    i32.store
                    block  ;; label = @9
                      local.get 3
                      i32.const 256
                      i32.lt_u
                      br_if 0 (;@9;)
                      local.get 0
                      local.get 3
                      call 40
                      br 6 (;@3;)
                    end
                    local.get 3
                    i32.const 248
                    i32.and
                    i32.const 9384
                    i32.add
                    local.set 2
                    block  ;; label = @9
                      block  ;; label = @10
                        i32.const 0
                        i32.load offset=9648
                        local.tee 6
                        i32.const 1
                        local.get 3
                        i32.const 3
                        i32.shr_u
                        i32.shl
                        local.tee 3
                        i32.and
                        br_if 0 (;@10;)
                        i32.const 0
                        local.get 6
                        local.get 3
                        i32.or
                        i32.store offset=9648
                        local.get 2
                        local.set 3
                        br 1 (;@9;)
                      end
                      local.get 2
                      i32.load offset=8
                      local.set 3
                    end
                    local.get 2
                    local.get 0
                    i32.store offset=8
                    local.get 3
                    local.get 0
                    i32.store offset=12
                    local.get 0
                    local.get 2
                    i32.store offset=12
                    local.get 0
                    local.get 3
                    i32.store offset=8
                    br 5 (;@3;)
                  end
                  i32.const 0
                  local.get 0
                  local.get 2
                  i32.sub
                  local.tee 3
                  i32.store offset=9660
                  i32.const 0
                  i32.const 0
                  i32.load offset=9668
                  local.tee 0
                  local.get 2
                  i32.add
                  local.tee 6
                  i32.store offset=9668
                  local.get 6
                  local.get 3
                  i32.const 1
                  i32.or
                  i32.store offset=4
                  local.get 0
                  local.get 2
                  i32.const 3
                  i32.or
                  i32.store offset=4
                  local.get 0
                  i32.const 8
                  i32.add
                  local.set 0
                  br 6 (;@1;)
                end
                i32.const 0
                i32.load offset=9664
                local.set 3
                block  ;; label = @7
                  block  ;; label = @8
                    local.get 0
                    local.get 2
                    i32.sub
                    local.tee 6
                    i32.const 15
                    i32.gt_u
                    br_if 0 (;@8;)
                    i32.const 0
                    i32.const 0
                    i32.store offset=9664
                    i32.const 0
                    i32.const 0
                    i32.store offset=9656
                    local.get 3
                    local.get 0
                    i32.const 3
                    i32.or
                    i32.store offset=4
                    local.get 3
                    local.get 0
                    i32.add
                    local.tee 0
                    local.get 0
                    i32.load offset=4
                    i32.const 1
                    i32.or
                    i32.store offset=4
                    br 1 (;@7;)
                  end
                  i32.const 0
                  local.get 6
                  i32.store offset=9656
                  i32.const 0
                  local.get 3
                  local.get 2
                  i32.add
                  local.tee 1
                  i32.store offset=9664
                  local.get 1
                  local.get 6
                  i32.const 1
                  i32.or
                  i32.store offset=4
                  local.get 3
                  local.get 0
                  i32.add
                  local.get 6
                  i32.store
                  local.get 3
                  local.get 2
                  i32.const 3
                  i32.or
                  i32.store offset=4
                end
                local.get 3
                i32.const 8
                i32.add
                return
              end
              local.get 0
              local.get 7
              local.get 8
              i32.add
              i32.store offset=4
              i32.const 0
              i32.const 0
              i32.load offset=9668
              local.tee 0
              i32.const 15
              i32.add
              i32.const -8
              i32.and
              local.tee 3
              i32.const -8
              i32.add
              local.tee 6
              i32.store offset=9668
              i32.const 0
              local.get 0
              local.get 3
              i32.sub
              i32.const 0
              i32.load offset=9660
              local.get 8
              i32.add
              local.tee 3
              i32.add
              i32.const 8
              i32.add
              local.tee 1
              i32.store offset=9660
              local.get 6
              local.get 1
              i32.const 1
              i32.or
              i32.store offset=4
              local.get 0
              local.get 3
              i32.add
              i32.const 40
              i32.store offset=4
              i32.const 0
              i32.const 2097152
              i32.store offset=9680
              br 3 (;@2;)
            end
            i32.const 0
            local.get 0
            i32.store offset=9668
            i32.const 0
            i32.const 0
            i32.load offset=9660
            local.get 3
            i32.add
            local.tee 3
            i32.store offset=9660
            local.get 0
            local.get 3
            i32.const 1
            i32.or
            i32.store offset=4
            br 1 (;@3;)
          end
          i32.const 0
          local.get 0
          i32.store offset=9664
          i32.const 0
          i32.const 0
          i32.load offset=9656
          local.get 3
          i32.add
          local.tee 3
          i32.store offset=9656
          local.get 0
          local.get 3
          i32.const 1
          i32.or
          i32.store offset=4
          local.get 0
          local.get 3
          i32.add
          local.get 3
          i32.store
        end
        local.get 1
        i32.const 8
        i32.add
        return
      end
      i32.const 0
      local.set 0
      i32.const 0
      i32.load offset=9660
      local.tee 3
      local.get 2
      i32.le_u
      br_if 0 (;@1;)
      i32.const 0
      local.get 3
      local.get 2
      i32.sub
      local.tee 3
      i32.store offset=9660
      i32.const 0
      i32.const 0
      i32.load offset=9668
      local.tee 0
      local.get 2
      i32.add
      local.tee 6
      i32.store offset=9668
      local.get 6
      local.get 3
      i32.const 1
      i32.or
      i32.store offset=4
      local.get 0
      local.get 2
      i32.const 3
      i32.or
      i32.store offset=4
      local.get 0
      i32.const 8
      i32.add
      return
    end
    local.get 0)
  (func (;40;) (type 2) (param i32 i32)
    (local i32 i32 i32 i32)
    i32.const 31
    local.set 2
    block  ;; label = @1
      local.get 1
      i32.const 16777215
      i32.gt_u
      br_if 0 (;@1;)
      local.get 1
      i32.const 6
      local.get 1
      i32.const 8
      i32.shr_u
      i32.clz
      local.tee 2
      i32.sub
      i32.shr_u
      i32.const 1
      i32.and
      local.get 2
      i32.const 1
      i32.shl
      i32.sub
      i32.const 62
      i32.add
      local.set 2
    end
    local.get 0
    i64.const 0
    i64.store offset=16 align=4
    local.get 0
    local.get 2
    i32.store offset=28
    local.get 2
    i32.const 2
    i32.shl
    i32.const 9240
    i32.add
    local.set 3
    block  ;; label = @1
      i32.const 0
      i32.load offset=9652
      i32.const 1
      local.get 2
      i32.shl
      local.tee 4
      i32.and
      br_if 0 (;@1;)
      local.get 3
      local.get 0
      i32.store
      local.get 0
      local.get 3
      i32.store offset=24
      local.get 0
      local.get 0
      i32.store offset=12
      local.get 0
      local.get 0
      i32.store offset=8
      i32.const 0
      i32.const 0
      i32.load offset=9652
      local.get 4
      i32.or
      i32.store offset=9652
      return
    end
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          local.get 3
          i32.load
          local.tee 4
          i32.load offset=4
          i32.const -8
          i32.and
          local.get 1
          i32.ne
          br_if 0 (;@3;)
          local.get 4
          local.set 2
          br 1 (;@2;)
        end
        local.get 1
        i32.const 0
        i32.const 25
        local.get 2
        i32.const 1
        i32.shr_u
        i32.sub
        local.get 2
        i32.const 31
        i32.eq
        select
        i32.shl
        local.set 3
        loop  ;; label = @3
          local.get 4
          local.get 3
          i32.const 29
          i32.shr_u
          i32.const 4
          i32.and
          i32.add
          local.tee 5
          i32.load offset=16
          local.tee 2
          i32.eqz
          br_if 2 (;@1;)
          local.get 3
          i32.const 1
          i32.shl
          local.set 3
          local.get 2
          local.set 4
          local.get 2
          i32.load offset=4
          i32.const -8
          i32.and
          local.get 1
          i32.ne
          br_if 0 (;@3;)
        end
      end
      local.get 2
      i32.load offset=8
      local.tee 3
      local.get 0
      i32.store offset=12
      local.get 2
      local.get 0
      i32.store offset=8
      local.get 0
      i32.const 0
      i32.store offset=24
      local.get 0
      local.get 2
      i32.store offset=12
      local.get 0
      local.get 3
      i32.store offset=8
      return
    end
    local.get 5
    i32.const 16
    i32.add
    local.get 0
    i32.store
    local.get 0
    local.get 4
    i32.store offset=24
    local.get 0
    local.get 0
    i32.store offset=12
    local.get 0
    local.get 0
    i32.store offset=8)
  (func (;41;) (type 2) (param i32 i32)
    (local i32 i32 i32 i32)
    local.get 0
    i32.load offset=12
    local.set 2
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            local.get 1
            i32.const 256
            i32.lt_u
            br_if 0 (;@4;)
            local.get 0
            i32.load offset=24
            local.set 3
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  local.get 2
                  local.get 0
                  i32.ne
                  br_if 0 (;@7;)
                  local.get 0
                  i32.const 20
                  i32.const 16
                  local.get 0
                  i32.load offset=20
                  local.tee 2
                  select
                  i32.add
                  i32.load
                  local.tee 1
                  br_if 1 (;@6;)
                  i32.const 0
                  local.set 2
                  br 2 (;@5;)
                end
                local.get 0
                i32.load offset=8
                local.tee 1
                local.get 2
                i32.store offset=12
                local.get 2
                local.get 1
                i32.store offset=8
                br 1 (;@5;)
              end
              local.get 0
              i32.const 20
              i32.add
              local.get 0
              i32.const 16
              i32.add
              local.get 2
              select
              local.set 4
              loop  ;; label = @6
                local.get 4
                local.set 5
                local.get 1
                local.tee 2
                i32.const 20
                i32.add
                local.get 2
                i32.const 16
                i32.add
                local.get 2
                i32.load offset=20
                local.tee 1
                select
                local.set 4
                local.get 2
                i32.const 20
                i32.const 16
                local.get 1
                select
                i32.add
                i32.load
                local.tee 1
                br_if 0 (;@6;)
              end
              local.get 5
              i32.const 0
              i32.store
            end
            local.get 3
            i32.eqz
            br_if 2 (;@2;)
            block  ;; label = @5
              block  ;; label = @6
                local.get 0
                local.get 0
                i32.load offset=28
                i32.const 2
                i32.shl
                i32.const 9240
                i32.add
                local.tee 1
                i32.load
                i32.eq
                br_if 0 (;@6;)
                local.get 3
                i32.load offset=16
                local.get 0
                i32.eq
                br_if 1 (;@5;)
                local.get 3
                local.get 2
                i32.store offset=20
                local.get 2
                br_if 3 (;@3;)
                br 4 (;@2;)
              end
              local.get 1
              local.get 2
              i32.store
              local.get 2
              i32.eqz
              br_if 4 (;@1;)
              br 2 (;@3;)
            end
            local.get 3
            local.get 2
            i32.store offset=16
            local.get 2
            br_if 1 (;@3;)
            br 2 (;@2;)
          end
          block  ;; label = @4
            local.get 2
            local.get 0
            i32.load offset=8
            local.tee 4
            i32.eq
            br_if 0 (;@4;)
            local.get 4
            local.get 2
            i32.store offset=12
            local.get 2
            local.get 4
            i32.store offset=8
            return
          end
          i32.const 0
          i32.const 0
          i32.load offset=9648
          i32.const -2
          local.get 1
          i32.const 3
          i32.shr_u
          i32.rotl
          i32.and
          i32.store offset=9648
          return
        end
        local.get 2
        local.get 3
        i32.store offset=24
        block  ;; label = @3
          local.get 0
          i32.load offset=16
          local.tee 1
          i32.eqz
          br_if 0 (;@3;)
          local.get 2
          local.get 1
          i32.store offset=16
          local.get 1
          local.get 2
          i32.store offset=24
        end
        local.get 0
        i32.load offset=20
        local.tee 1
        i32.eqz
        br_if 0 (;@2;)
        local.get 2
        local.get 1
        i32.store offset=20
        local.get 1
        local.get 2
        i32.store offset=24
        return
      end
      return
    end
    i32.const 0
    i32.const 0
    i32.load offset=9652
    i32.const -2
    local.get 0
    i32.load offset=28
    i32.rotl
    i32.and
    i32.store offset=9652)
  (func (;42;) (type 2) (param i32 i32)
    (local i32 i32 i32 i32)
    local.get 0
    local.get 1
    i32.add
    local.set 2
    block  ;; label = @1
      block  ;; label = @2
        local.get 0
        i32.load offset=4
        local.tee 3
        i32.const 1
        i32.and
        br_if 0 (;@2;)
        local.get 3
        i32.const 2
        i32.and
        i32.eqz
        br_if 1 (;@1;)
        local.get 0
        i32.load
        local.tee 3
        local.get 1
        i32.add
        local.set 1
        block  ;; label = @3
          local.get 0
          local.get 3
          i32.sub
          local.tee 0
          i32.const 0
          i32.load offset=9664
          i32.ne
          br_if 0 (;@3;)
          local.get 2
          i32.load offset=4
          i32.const 3
          i32.and
          i32.const 3
          i32.ne
          br_if 1 (;@2;)
          i32.const 0
          local.get 1
          i32.store offset=9656
          local.get 2
          local.get 2
          i32.load offset=4
          i32.const -2
          i32.and
          i32.store offset=4
          local.get 0
          local.get 1
          i32.const 1
          i32.or
          i32.store offset=4
          local.get 2
          local.get 1
          i32.store
          br 2 (;@1;)
        end
        local.get 0
        local.get 3
        call 41
      end
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              local.get 2
              i32.load offset=4
              local.tee 3
              i32.const 2
              i32.and
              br_if 0 (;@5;)
              local.get 2
              i32.const 0
              i32.load offset=9668
              i32.eq
              br_if 2 (;@3;)
              local.get 2
              i32.const 0
              i32.load offset=9664
              i32.eq
              br_if 3 (;@2;)
              local.get 2
              local.get 3
              i32.const -8
              i32.and
              local.tee 3
              call 41
              local.get 0
              local.get 3
              local.get 1
              i32.add
              local.tee 1
              i32.const 1
              i32.or
              i32.store offset=4
              local.get 0
              local.get 1
              i32.add
              local.get 1
              i32.store
              local.get 0
              i32.const 0
              i32.load offset=9664
              i32.ne
              br_if 1 (;@4;)
              i32.const 0
              local.get 1
              i32.store offset=9656
              return
            end
            local.get 2
            local.get 3
            i32.const -2
            i32.and
            i32.store offset=4
            local.get 0
            local.get 1
            i32.const 1
            i32.or
            i32.store offset=4
            local.get 0
            local.get 1
            i32.add
            local.get 1
            i32.store
          end
          block  ;; label = @4
            local.get 1
            i32.const 256
            i32.lt_u
            br_if 0 (;@4;)
            i32.const 31
            local.set 2
            block  ;; label = @5
              local.get 1
              i32.const 16777215
              i32.gt_u
              br_if 0 (;@5;)
              local.get 1
              i32.const 6
              local.get 1
              i32.const 8
              i32.shr_u
              i32.clz
              local.tee 2
              i32.sub
              i32.shr_u
              i32.const 1
              i32.and
              local.get 2
              i32.const 1
              i32.shl
              i32.sub
              i32.const 62
              i32.add
              local.set 2
            end
            local.get 0
            i64.const 0
            i64.store offset=16 align=4
            local.get 0
            local.get 2
            i32.store offset=28
            local.get 2
            i32.const 2
            i32.shl
            i32.const 9240
            i32.add
            local.set 3
            block  ;; label = @5
              i32.const 0
              i32.load offset=9652
              i32.const 1
              local.get 2
              i32.shl
              local.tee 4
              i32.and
              br_if 0 (;@5;)
              local.get 3
              local.get 0
              i32.store
              local.get 0
              local.get 3
              i32.store offset=24
              local.get 0
              local.get 0
              i32.store offset=12
              local.get 0
              local.get 0
              i32.store offset=8
              i32.const 0
              i32.const 0
              i32.load offset=9652
              local.get 4
              i32.or
              i32.store offset=9652
              return
            end
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  local.get 3
                  i32.load
                  local.tee 4
                  i32.load offset=4
                  i32.const -8
                  i32.and
                  local.get 1
                  i32.ne
                  br_if 0 (;@7;)
                  local.get 4
                  local.set 2
                  br 1 (;@6;)
                end
                local.get 1
                i32.const 0
                i32.const 25
                local.get 2
                i32.const 1
                i32.shr_u
                i32.sub
                local.get 2
                i32.const 31
                i32.eq
                select
                i32.shl
                local.set 3
                loop  ;; label = @7
                  local.get 4
                  local.get 3
                  i32.const 29
                  i32.shr_u
                  i32.const 4
                  i32.and
                  i32.add
                  local.tee 5
                  i32.load offset=16
                  local.tee 2
                  i32.eqz
                  br_if 2 (;@5;)
                  local.get 3
                  i32.const 1
                  i32.shl
                  local.set 3
                  local.get 2
                  local.set 4
                  local.get 2
                  i32.load offset=4
                  i32.const -8
                  i32.and
                  local.get 1
                  i32.ne
                  br_if 0 (;@7;)
                end
              end
              local.get 2
              i32.load offset=8
              local.tee 1
              local.get 0
              i32.store offset=12
              local.get 2
              local.get 0
              i32.store offset=8
              local.get 0
              i32.const 0
              i32.store offset=24
              local.get 0
              local.get 2
              i32.store offset=12
              local.get 0
              local.get 1
              i32.store offset=8
              return
            end
            local.get 5
            i32.const 16
            i32.add
            local.get 0
            i32.store
            local.get 0
            local.get 4
            i32.store offset=24
            local.get 0
            local.get 0
            i32.store offset=12
            local.get 0
            local.get 0
            i32.store offset=8
            return
          end
          local.get 1
          i32.const 248
          i32.and
          i32.const 9384
          i32.add
          local.set 2
          block  ;; label = @4
            block  ;; label = @5
              i32.const 0
              i32.load offset=9648
              local.tee 3
              i32.const 1
              local.get 1
              i32.const 3
              i32.shr_u
              i32.shl
              local.tee 1
              i32.and
              br_if 0 (;@5;)
              i32.const 0
              local.get 3
              local.get 1
              i32.or
              i32.store offset=9648
              local.get 2
              local.set 1
              br 1 (;@4;)
            end
            local.get 2
            i32.load offset=8
            local.set 1
          end
          local.get 2
          local.get 0
          i32.store offset=8
          local.get 1
          local.get 0
          i32.store offset=12
          local.get 0
          local.get 2
          i32.store offset=12
          local.get 0
          local.get 1
          i32.store offset=8
          return
        end
        i32.const 0
        local.get 0
        i32.store offset=9668
        i32.const 0
        i32.const 0
        i32.load offset=9660
        local.get 1
        i32.add
        local.tee 1
        i32.store offset=9660
        local.get 0
        local.get 1
        i32.const 1
        i32.or
        i32.store offset=4
        local.get 0
        i32.const 0
        i32.load offset=9664
        i32.ne
        br_if 1 (;@1;)
        i32.const 0
        i32.const 0
        i32.store offset=9656
        i32.const 0
        i32.const 0
        i32.store offset=9664
        return
      end
      i32.const 0
      local.get 0
      i32.store offset=9664
      i32.const 0
      i32.const 0
      i32.load offset=9656
      local.get 1
      i32.add
      local.tee 1
      i32.store offset=9656
      local.get 0
      local.get 1
      i32.const 1
      i32.or
      i32.store offset=4
      local.get 0
      local.get 1
      i32.add
      local.get 1
      i32.store
      return
    end)
  (func (;43;) (type 4) (param i32)
    (local i32 i32 i32 i32 i32)
    local.get 0
    i32.const -8
    i32.add
    local.tee 1
    local.get 0
    i32.const -4
    i32.add
    i32.load
    local.tee 2
    i32.const -8
    i32.and
    local.tee 0
    i32.add
    local.set 3
    block  ;; label = @1
      block  ;; label = @2
        local.get 2
        i32.const 1
        i32.and
        br_if 0 (;@2;)
        local.get 2
        i32.const 2
        i32.and
        i32.eqz
        br_if 1 (;@1;)
        local.get 1
        i32.load
        local.tee 2
        local.get 0
        i32.add
        local.set 0
        block  ;; label = @3
          local.get 1
          local.get 2
          i32.sub
          local.tee 1
          i32.const 0
          i32.load offset=9664
          i32.ne
          br_if 0 (;@3;)
          local.get 3
          i32.load offset=4
          i32.const 3
          i32.and
          i32.const 3
          i32.ne
          br_if 1 (;@2;)
          i32.const 0
          local.get 0
          i32.store offset=9656
          local.get 3
          local.get 3
          i32.load offset=4
          i32.const -2
          i32.and
          i32.store offset=4
          local.get 1
          local.get 0
          i32.const 1
          i32.or
          i32.store offset=4
          local.get 3
          local.get 0
          i32.store
          return
        end
        local.get 1
        local.get 2
        call 41
      end
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  block  ;; label = @8
                    block  ;; label = @9
                      local.get 3
                      i32.load offset=4
                      local.tee 2
                      i32.const 2
                      i32.and
                      br_if 0 (;@9;)
                      local.get 3
                      i32.const 0
                      i32.load offset=9668
                      i32.eq
                      br_if 2 (;@7;)
                      local.get 3
                      i32.const 0
                      i32.load offset=9664
                      i32.eq
                      br_if 3 (;@6;)
                      local.get 3
                      local.get 2
                      i32.const -8
                      i32.and
                      local.tee 2
                      call 41
                      local.get 1
                      local.get 2
                      local.get 0
                      i32.add
                      local.tee 0
                      i32.const 1
                      i32.or
                      i32.store offset=4
                      local.get 1
                      local.get 0
                      i32.add
                      local.get 0
                      i32.store
                      local.get 1
                      i32.const 0
                      i32.load offset=9664
                      i32.ne
                      br_if 1 (;@8;)
                      i32.const 0
                      local.get 0
                      i32.store offset=9656
                      return
                    end
                    local.get 3
                    local.get 2
                    i32.const -2
                    i32.and
                    i32.store offset=4
                    local.get 1
                    local.get 0
                    i32.const 1
                    i32.or
                    i32.store offset=4
                    local.get 1
                    local.get 0
                    i32.add
                    local.get 0
                    i32.store
                  end
                  local.get 0
                  i32.const 256
                  i32.lt_u
                  br_if 2 (;@5;)
                  i32.const 31
                  local.set 3
                  block  ;; label = @8
                    local.get 0
                    i32.const 16777215
                    i32.gt_u
                    br_if 0 (;@8;)
                    local.get 0
                    i32.const 6
                    local.get 0
                    i32.const 8
                    i32.shr_u
                    i32.clz
                    local.tee 3
                    i32.sub
                    i32.shr_u
                    i32.const 1
                    i32.and
                    local.get 3
                    i32.const 1
                    i32.shl
                    i32.sub
                    i32.const 62
                    i32.add
                    local.set 3
                  end
                  local.get 1
                  i64.const 0
                  i64.store offset=16 align=4
                  local.get 1
                  local.get 3
                  i32.store offset=28
                  local.get 3
                  i32.const 2
                  i32.shl
                  i32.const 9240
                  i32.add
                  local.set 2
                  i32.const 0
                  i32.load offset=9652
                  i32.const 1
                  local.get 3
                  i32.shl
                  local.tee 4
                  i32.and
                  br_if 3 (;@4;)
                  local.get 2
                  local.get 1
                  i32.store
                  local.get 1
                  local.get 2
                  i32.store offset=24
                  local.get 1
                  local.get 1
                  i32.store offset=12
                  local.get 1
                  local.get 1
                  i32.store offset=8
                  i32.const 0
                  i32.const 0
                  i32.load offset=9652
                  local.get 4
                  i32.or
                  i32.store offset=9652
                  br 4 (;@3;)
                end
                i32.const 0
                local.get 1
                i32.store offset=9668
                i32.const 0
                i32.const 0
                i32.load offset=9660
                local.get 0
                i32.add
                local.tee 0
                i32.store offset=9660
                local.get 1
                local.get 0
                i32.const 1
                i32.or
                i32.store offset=4
                block  ;; label = @7
                  local.get 1
                  i32.const 0
                  i32.load offset=9664
                  i32.ne
                  br_if 0 (;@7;)
                  i32.const 0
                  i32.const 0
                  i32.store offset=9656
                  i32.const 0
                  i32.const 0
                  i32.store offset=9664
                end
                local.get 0
                i32.const 0
                i32.load offset=9680
                local.tee 4
                i32.le_u
                br_if 5 (;@1;)
                i32.const 0
                i32.load offset=9668
                local.tee 0
                i32.eqz
                br_if 5 (;@1;)
                i32.const 0
                local.set 2
                i32.const 0
                i32.load offset=9660
                local.tee 5
                i32.const 41
                i32.lt_u
                br_if 4 (;@2;)
                i32.const 9368
                local.set 1
                loop  ;; label = @7
                  block  ;; label = @8
                    local.get 1
                    i32.load
                    local.tee 3
                    local.get 0
                    i32.gt_u
                    br_if 0 (;@8;)
                    local.get 0
                    local.get 3
                    local.get 1
                    i32.load offset=4
                    i32.add
                    i32.lt_u
                    br_if 6 (;@2;)
                  end
                  local.get 1
                  i32.load offset=8
                  local.set 1
                  br 0 (;@7;)
                end
              end
              i32.const 0
              local.get 1
              i32.store offset=9664
              i32.const 0
              i32.const 0
              i32.load offset=9656
              local.get 0
              i32.add
              local.tee 0
              i32.store offset=9656
              local.get 1
              local.get 0
              i32.const 1
              i32.or
              i32.store offset=4
              local.get 1
              local.get 0
              i32.add
              local.get 0
              i32.store
              return
            end
            local.get 0
            i32.const 248
            i32.and
            i32.const 9384
            i32.add
            local.set 3
            block  ;; label = @5
              block  ;; label = @6
                i32.const 0
                i32.load offset=9648
                local.tee 2
                i32.const 1
                local.get 0
                i32.const 3
                i32.shr_u
                i32.shl
                local.tee 0
                i32.and
                br_if 0 (;@6;)
                i32.const 0
                local.get 2
                local.get 0
                i32.or
                i32.store offset=9648
                local.get 3
                local.set 0
                br 1 (;@5;)
              end
              local.get 3
              i32.load offset=8
              local.set 0
            end
            local.get 3
            local.get 1
            i32.store offset=8
            local.get 0
            local.get 1
            i32.store offset=12
            local.get 1
            local.get 3
            i32.store offset=12
            local.get 1
            local.get 0
            i32.store offset=8
            return
          end
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                local.get 2
                i32.load
                local.tee 4
                i32.load offset=4
                i32.const -8
                i32.and
                local.get 0
                i32.ne
                br_if 0 (;@6;)
                local.get 4
                local.set 3
                br 1 (;@5;)
              end
              local.get 0
              i32.const 0
              i32.const 25
              local.get 3
              i32.const 1
              i32.shr_u
              i32.sub
              local.get 3
              i32.const 31
              i32.eq
              select
              i32.shl
              local.set 2
              loop  ;; label = @6
                local.get 4
                local.get 2
                i32.const 29
                i32.shr_u
                i32.const 4
                i32.and
                i32.add
                local.tee 5
                i32.load offset=16
                local.tee 3
                i32.eqz
                br_if 2 (;@4;)
                local.get 2
                i32.const 1
                i32.shl
                local.set 2
                local.get 3
                local.set 4
                local.get 3
                i32.load offset=4
                i32.const -8
                i32.and
                local.get 0
                i32.ne
                br_if 0 (;@6;)
              end
            end
            local.get 3
            i32.load offset=8
            local.tee 0
            local.get 1
            i32.store offset=12
            local.get 3
            local.get 1
            i32.store offset=8
            local.get 1
            i32.const 0
            i32.store offset=24
            local.get 1
            local.get 3
            i32.store offset=12
            local.get 1
            local.get 0
            i32.store offset=8
            br 1 (;@3;)
          end
          local.get 5
          i32.const 16
          i32.add
          local.get 1
          i32.store
          local.get 1
          local.get 4
          i32.store offset=24
          local.get 1
          local.get 1
          i32.store offset=12
          local.get 1
          local.get 1
          i32.store offset=8
        end
        i32.const 0
        local.set 1
        i32.const 0
        i32.const 0
        i32.load offset=9688
        i32.const -1
        i32.add
        local.tee 0
        i32.store offset=9688
        local.get 0
        br_if 1 (;@1;)
        block  ;; label = @3
          i32.const 0
          i32.load offset=9376
          local.tee 0
          i32.eqz
          br_if 0 (;@3;)
          i32.const 0
          local.set 1
          loop  ;; label = @4
            local.get 1
            i32.const 1
            i32.add
            local.set 1
            local.get 0
            i32.load offset=8
            local.tee 0
            br_if 0 (;@4;)
          end
        end
        i32.const 0
        local.get 1
        i32.const 4095
        local.get 1
        i32.const 4095
        i32.gt_u
        select
        i32.store offset=9688
        return
      end
      block  ;; label = @2
        i32.const 0
        i32.load offset=9376
        local.tee 1
        i32.eqz
        br_if 0 (;@2;)
        i32.const 0
        local.set 2
        loop  ;; label = @3
          local.get 2
          i32.const 1
          i32.add
          local.set 2
          local.get 1
          i32.load offset=8
          local.tee 1
          br_if 0 (;@3;)
        end
      end
      i32.const 0
      local.get 2
      i32.const 4095
      local.get 2
      i32.const 4095
      i32.gt_u
      select
      i32.store offset=9688
      local.get 5
      local.get 4
      i32.le_u
      br_if 0 (;@1;)
      i32.const 0
      i32.const -1
      i32.store offset=9680
    end)
  (func (;44;) (type 1) (param i32 i32 i32) (result i32)
    (local i32 i32 i32 i32 i32)
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          local.get 0
          i32.const -4
          i32.add
          local.tee 3
          i32.load
          local.tee 4
          i32.const -8
          i32.and
          local.tee 5
          i32.const 4
          i32.const 8
          local.get 4
          i32.const 3
          i32.and
          local.tee 6
          select
          local.get 1
          i32.add
          i32.lt_u
          br_if 0 (;@3;)
          block  ;; label = @4
            local.get 6
            i32.eqz
            br_if 0 (;@4;)
            local.get 5
            local.get 1
            i32.const 39
            i32.add
            i32.gt_u
            br_if 2 (;@2;)
          end
          i32.const 16
          local.get 2
          i32.const 11
          i32.add
          i32.const -8
          i32.and
          local.get 2
          i32.const 11
          i32.lt_u
          select
          local.set 1
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                local.get 6
                br_if 0 (;@6;)
                local.get 1
                i32.const 256
                i32.lt_u
                br_if 1 (;@5;)
                local.get 5
                local.get 1
                i32.const 4
                i32.or
                i32.lt_u
                br_if 1 (;@5;)
                local.get 5
                local.get 1
                i32.sub
                i32.const 131073
                i32.ge_u
                br_if 1 (;@5;)
                br 2 (;@4;)
              end
              local.get 0
              i32.const -8
              i32.add
              local.tee 7
              local.get 5
              i32.add
              local.set 6
              block  ;; label = @6
                block  ;; label = @7
                  block  ;; label = @8
                    block  ;; label = @9
                      local.get 5
                      local.get 1
                      i32.ge_u
                      br_if 0 (;@9;)
                      local.get 6
                      i32.const 0
                      i32.load offset=9668
                      i32.eq
                      br_if 3 (;@6;)
                      local.get 6
                      i32.const 0
                      i32.load offset=9664
                      i32.eq
                      br_if 2 (;@7;)
                      local.get 6
                      i32.load offset=4
                      local.tee 4
                      i32.const 2
                      i32.and
                      br_if 4 (;@5;)
                      local.get 4
                      i32.const -8
                      i32.and
                      local.tee 4
                      local.get 5
                      i32.add
                      local.tee 5
                      local.get 1
                      i32.lt_u
                      br_if 4 (;@5;)
                      local.get 6
                      local.get 4
                      call 41
                      local.get 5
                      local.get 1
                      i32.sub
                      local.tee 2
                      i32.const 16
                      i32.lt_u
                      br_if 1 (;@8;)
                      local.get 3
                      local.get 1
                      local.get 3
                      i32.load
                      i32.const 1
                      i32.and
                      i32.or
                      i32.const 2
                      i32.or
                      i32.store
                      local.get 7
                      local.get 1
                      i32.add
                      local.tee 1
                      local.get 2
                      i32.const 3
                      i32.or
                      i32.store offset=4
                      local.get 7
                      local.get 5
                      i32.add
                      local.tee 5
                      local.get 5
                      i32.load offset=4
                      i32.const 1
                      i32.or
                      i32.store offset=4
                      local.get 1
                      local.get 2
                      call 42
                      local.get 0
                      return
                    end
                    local.get 5
                    local.get 1
                    i32.sub
                    local.tee 2
                    i32.const 15
                    i32.le_u
                    br_if 4 (;@4;)
                    local.get 3
                    local.get 1
                    local.get 4
                    i32.const 1
                    i32.and
                    i32.or
                    i32.const 2
                    i32.or
                    i32.store
                    local.get 7
                    local.get 1
                    i32.add
                    local.tee 5
                    local.get 2
                    i32.const 3
                    i32.or
                    i32.store offset=4
                    local.get 6
                    local.get 6
                    i32.load offset=4
                    i32.const 1
                    i32.or
                    i32.store offset=4
                    local.get 5
                    local.get 2
                    call 42
                    local.get 0
                    return
                  end
                  local.get 3
                  local.get 5
                  local.get 3
                  i32.load
                  i32.const 1
                  i32.and
                  i32.or
                  i32.const 2
                  i32.or
                  i32.store
                  local.get 7
                  local.get 5
                  i32.add
                  local.tee 2
                  local.get 2
                  i32.load offset=4
                  i32.const 1
                  i32.or
                  i32.store offset=4
                  local.get 0
                  return
                end
                i32.const 0
                i32.load offset=9656
                local.get 5
                i32.add
                local.tee 5
                local.get 1
                i32.lt_u
                br_if 1 (;@5;)
                block  ;; label = @7
                  block  ;; label = @8
                    local.get 5
                    local.get 1
                    i32.sub
                    local.tee 2
                    i32.const 15
                    i32.gt_u
                    br_if 0 (;@8;)
                    local.get 3
                    local.get 4
                    i32.const 1
                    i32.and
                    local.get 5
                    i32.or
                    i32.const 2
                    i32.or
                    i32.store
                    local.get 7
                    local.get 5
                    i32.add
                    local.tee 2
                    local.get 2
                    i32.load offset=4
                    i32.const 1
                    i32.or
                    i32.store offset=4
                    i32.const 0
                    local.set 2
                    i32.const 0
                    local.set 1
                    br 1 (;@7;)
                  end
                  local.get 3
                  local.get 1
                  local.get 4
                  i32.const 1
                  i32.and
                  i32.or
                  i32.const 2
                  i32.or
                  i32.store
                  local.get 7
                  local.get 1
                  i32.add
                  local.tee 1
                  local.get 2
                  i32.const 1
                  i32.or
                  i32.store offset=4
                  local.get 7
                  local.get 5
                  i32.add
                  local.tee 5
                  local.get 2
                  i32.store
                  local.get 5
                  local.get 5
                  i32.load offset=4
                  i32.const -2
                  i32.and
                  i32.store offset=4
                end
                i32.const 0
                local.get 1
                i32.store offset=9664
                i32.const 0
                local.get 2
                i32.store offset=9656
                local.get 0
                return
              end
              i32.const 0
              i32.load offset=9660
              local.get 5
              i32.add
              local.tee 5
              local.get 1
              i32.gt_u
              br_if 4 (;@1;)
            end
            block  ;; label = @5
              local.get 2
              call 39
              local.tee 5
              br_if 0 (;@5;)
              i32.const 0
              return
            end
            block  ;; label = @5
              local.get 2
              i32.const -4
              i32.const -8
              local.get 3
              i32.load
              local.tee 1
              i32.const 3
              i32.and
              select
              local.get 1
              i32.const -8
              i32.and
              i32.add
              local.tee 1
              local.get 2
              local.get 1
              i32.lt_u
              select
              local.tee 2
              i32.eqz
              br_if 0 (;@5;)
              local.get 5
              local.get 0
              local.get 2
              memory.copy
            end
            local.get 0
            call 43
            local.get 5
            local.set 0
          end
          local.get 0
          return
        end
        i32.const 8833
        i32.const 46
        i32.const 8880
        call 31
        unreachable
      end
      i32.const 8896
      i32.const 46
      i32.const 8944
      call 31
      unreachable
    end
    local.get 3
    local.get 1
    local.get 4
    i32.const 1
    i32.and
    i32.or
    i32.const 2
    i32.or
    i32.store
    local.get 7
    local.get 1
    i32.add
    local.tee 2
    local.get 5
    local.get 1
    i32.sub
    local.tee 5
    i32.const 1
    i32.or
    i32.store offset=4
    i32.const 0
    local.get 5
    i32.store offset=9660
    i32.const 0
    local.get 2
    i32.store offset=9668
    local.get 0)
  (func (;45;) (type 7) (param i32 i32 i32)
    (local i32)
    block  ;; label = @1
      block  ;; label = @2
        local.get 2
        i32.load offset=4
        i32.eqz
        br_if 0 (;@2;)
        block  ;; label = @3
          local.get 2
          i32.load offset=8
          local.tee 3
          br_if 0 (;@3;)
          i32.const 0
          i32.load8_u offset=9697
          drop
          local.get 1
          i32.const 1
          call 13
          local.set 2
          br 2 (;@1;)
        end
        local.get 2
        i32.load
        local.get 3
        local.get 1
        call 44
        local.set 2
        br 1 (;@1;)
      end
      i32.const 0
      i32.load8_u offset=9697
      drop
      local.get 1
      i32.const 1
      call 13
      local.set 2
    end
    local.get 0
    local.get 1
    i32.store offset=8
    local.get 0
    local.get 2
    i32.const 1
    local.get 2
    select
    i32.store offset=4
    local.get 0
    local.get 2
    i32.eqz
    i32.store)
  (func (;46;) (type 1) (param i32 i32 i32) (result i32)
    (local i32 i32 i32)
    i32.const 0
    local.set 3
    block  ;; label = @1
      local.get 2
      i32.eqz
      br_if 0 (;@1;)
      block  ;; label = @2
        loop  ;; label = @3
          local.get 0
          i32.load8_u
          local.tee 4
          local.get 1
          i32.load8_u
          local.tee 5
          i32.ne
          br_if 1 (;@2;)
          local.get 0
          i32.const 1
          i32.add
          local.set 0
          local.get 1
          i32.const 1
          i32.add
          local.set 1
          local.get 2
          i32.const -1
          i32.add
          local.tee 2
          i32.eqz
          br_if 2 (;@1;)
          br 0 (;@3;)
        end
      end
      local.get 4
      local.get 5
      i32.sub
      local.set 3
    end
    local.get 3)
  (table (;0;) 5 5 funcref)
  (memory (;0;) 1)
  (global (;0;) (mut i32) (i32.const 8192))
  (global (;1;) i32 (i32.const 9698))
  (global (;2;) i32 (i32.const 9712))
  (export "memory" (memory 0))
  (export "mark_used" (func 9))
  (export "user_entrypoint" (func 12))
  (export "__data_end" (global 1))
  (export "__heap_base" (global 2))
  (elem (;0;) (i32.const 1) func 25 32 33 38)
  (data (;0;) (i32.const 8192) "/Users/prytikov/.rustup/toolchains/1.88.0-aarch64-apple-darwin/lib/rustlib/src/rust/library/alloc/src/slice.rs\00\00\00 \00\00n\00\00\00\be\01\00\00\1d\00\00\00src/main.rs\00\80 \00\00\0b\00\00\00\0a\00\00\00\15\00\00\00\80 \00\00\0b\00\00\00\0d\00\00\004\00\00\00\80 \00\00\0b\00\00\00\12\00\00\00*\00\00\00\80 \00\00\0b\00\00\00\08\00\00\00\01\00\00\00capacity overflow\00\00\00\cc \00\00\11\00\00\00\01\00\00\00\00\00\00\00explicit panic\00\00\f0 \00\00\0e\00\00\00index out of bounds: the len is  but the index is \00\00\08!\00\00 \00\00\00(!\00\00\12\00\00\0000010203040506070809101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899 out of range for slice of length range end index \00\006\22\00\00\10\00\00\00\14\22\00\00\22\00\00\00/rust/deps/dlmalloc-0.2.8/src/dlmalloc.rsassertion failed: psize >= size + min_overhead\00X\22\00\00)\00\00\00\ac\04\00\00\09\00\00\00assertion failed: psize <= size + max_overhead\00\00X\22\00\00)\00\00\00\b2\04\00\00\0d\00\00\00/Users/prytikov/.rustup/toolchains/1.88.0-aarch64-apple-darwin/lib/rustlib/src/rust/library/alloc/src/raw_vec/mod.rs\00#\00\00t\00\00\00.\02\00\00\11\00\00\00/Users/prytikov/Code/arbitrum-nitro/arbitrator/langs/rust/stylus-sdk/src/contract.rs\84#\00\00T\00\00\00\19\00\00\00\15\00\00\00\84#\00\00T\00\00\00.\00\00\00\14\00\00\00\00\00\00\00\00\00\00\00\00\00\00\00\00\00\00\00\00\00\00\00")
  (data (;1;) (i32.const 9228) "\02"))
