(module
  (type (;0;) (func (result i32)))
  (type (;1;) (func (param i32)))
  (type (;2;) (func (param i32 i32)))
  (type (;3;) (func))
  (type (;4;) (func (param i32) (result i32)))
  (import "vm_hooks" "msg_reentrant" (func (;0;) (type 0)))
  (import "vm_hooks" "read_args" (func (;1;) (type 1)))
  (import "vm_hooks" "storage_load_bytes32" (func (;2;) (type 2)))
  (import "vm_hooks" "transient_load_bytes32" (func (;3;) (type 2)))
  (import "vm_hooks" "storage_cache_bytes32" (func (;4;) (type 2)))
  (import "vm_hooks" "transient_store_bytes32" (func (;5;) (type 2)))
  (import "vm_hooks" "storage_flush_cache" (func (;6;) (type 1)))
  (import "vm_hooks" "write_result" (func (;7;) (type 2)))
  (import "vm_hooks" "pay_for_memory_grow" (func (;8;) (type 1)))
  (func (;9;) (type 3)
    call 10
    unreachable)
  (func (;10;) (type 3)
    i32.const 0
    call 8)
  (func (;11;) (type 4) (param i32) (result i32)
    (local i32 i32 i32 i64 i64 i64 i64 i32 i32)
    global.get 0
    i32.const 160
    i32.sub
    local.tee 1
    global.set 0
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          i32.const 0
          i32.load8_u offset=8192
          local.tee 2
          i32.const 2
          i32.ne
          br_if 0 (;@3;)
          i32.const 0
          call 0
          local.tee 2
          i32.store8 offset=8192
          i32.const 1
          local.set 3
          local.get 2
          i32.eqz
          br_if 1 (;@2;)
          br 2 (;@1;)
        end
        i32.const 1
        local.set 3
        local.get 2
        i32.const 1
        i32.and
        br_if 1 (;@1;)
      end
      block  ;; label = @2
        local.get 0
        i32.const -1
        i32.le_s
        br_if 0 (;@2;)
        block  ;; label = @3
          local.get 0
          br_if 0 (;@3;)
          i32.const 1
          call 1
          br 1 (;@2;)
        end
        i32.const 0
        i32.load8_u offset=8648
        drop
        local.get 0
        call 12
        local.tee 3
        i32.eqz
        br_if 0 (;@2;)
        local.get 3
        call 1
        local.get 0
        i32.const 32
        i32.le_u
        br_if 0 (;@2;)
        local.get 1
        i32.const 24
        i32.add
        local.get 3
        i32.const 25
        i32.add
        i64.load align=1
        i64.store
        local.get 1
        i32.const 16
        i32.add
        local.get 3
        i32.const 17
        i32.add
        i64.load align=1
        i64.store
        local.get 1
        i32.const 8
        i32.add
        local.get 3
        i32.const 9
        i32.add
        i64.load align=1
        i64.store
        local.get 1
        local.get 3
        i64.load offset=1 align=1
        i64.store
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  block  ;; label = @8
                    local.get 3
                    i32.load8_u
                    br_table 0 (;@8;) 2 (;@6;) 1 (;@7;) 3 (;@5;)
                  end
                  local.get 1
                  i64.load
                  local.set 4
                  local.get 1
                  i64.load offset=8
                  local.set 5
                  local.get 1
                  i64.load offset=16
                  local.set 6
                  local.get 1
                  i64.load offset=24
                  local.set 7
                  local.get 1
                  i32.const 96
                  i32.add
                  i32.const 24
                  i32.add
                  local.tee 2
                  i64.const 0
                  i64.store
                  local.get 1
                  i32.const 96
                  i32.add
                  i32.const 16
                  i32.add
                  local.tee 8
                  i64.const 0
                  i64.store
                  local.get 1
                  i32.const 96
                  i32.add
                  i32.const 8
                  i32.add
                  local.tee 9
                  i64.const 0
                  i64.store
                  local.get 1
                  i64.const 0
                  i64.store offset=96
                  local.get 1
                  local.get 7
                  i64.const 56
                  i64.shr_u
                  i64.store8 offset=159
                  local.get 1
                  local.get 7
                  i64.const 48
                  i64.shr_u
                  i64.store8 offset=158
                  local.get 1
                  local.get 7
                  i64.const 40
                  i64.shr_u
                  i64.store8 offset=157
                  local.get 1
                  local.get 7
                  i64.const 32
                  i64.shr_u
                  i64.store8 offset=156
                  local.get 1
                  local.get 7
                  i64.const 24
                  i64.shr_u
                  i64.store8 offset=155
                  local.get 1
                  local.get 7
                  i64.const 16
                  i64.shr_u
                  i64.store8 offset=154
                  local.get 1
                  local.get 7
                  i64.const 8
                  i64.shr_u
                  i64.store8 offset=153
                  local.get 1
                  local.get 7
                  i64.store8 offset=152
                  local.get 1
                  local.get 6
                  i64.const 56
                  i64.shr_u
                  i64.store8 offset=151
                  local.get 1
                  local.get 6
                  i64.const 48
                  i64.shr_u
                  i64.store8 offset=150
                  local.get 1
                  local.get 6
                  i64.const 40
                  i64.shr_u
                  i64.store8 offset=149
                  local.get 1
                  local.get 6
                  i64.const 32
                  i64.shr_u
                  i64.store8 offset=148
                  local.get 1
                  local.get 6
                  i64.const 24
                  i64.shr_u
                  i64.store8 offset=147
                  local.get 1
                  local.get 6
                  i64.const 16
                  i64.shr_u
                  i64.store8 offset=146
                  local.get 1
                  local.get 6
                  i64.const 8
                  i64.shr_u
                  i64.store8 offset=145
                  local.get 1
                  local.get 6
                  i64.store8 offset=144
                  local.get 1
                  local.get 5
                  i64.const 56
                  i64.shr_u
                  i64.store8 offset=143
                  local.get 1
                  local.get 5
                  i64.const 48
                  i64.shr_u
                  i64.store8 offset=142
                  local.get 1
                  local.get 5
                  i64.const 40
                  i64.shr_u
                  i64.store8 offset=141
                  local.get 1
                  local.get 5
                  i64.const 32
                  i64.shr_u
                  i64.store8 offset=140
                  local.get 1
                  local.get 5
                  i64.const 24
                  i64.shr_u
                  i64.store8 offset=139
                  local.get 1
                  local.get 5
                  i64.const 16
                  i64.shr_u
                  i64.store8 offset=138
                  local.get 1
                  local.get 5
                  i64.const 8
                  i64.shr_u
                  i64.store8 offset=137
                  local.get 1
                  local.get 5
                  i64.store8 offset=136
                  local.get 1
                  local.get 4
                  i64.const 56
                  i64.shr_u
                  i64.store8 offset=135
                  local.get 1
                  local.get 4
                  i64.const 48
                  i64.shr_u
                  i64.store8 offset=134
                  local.get 1
                  local.get 4
                  i64.const 40
                  i64.shr_u
                  i64.store8 offset=133
                  local.get 1
                  local.get 4
                  i64.const 32
                  i64.shr_u
                  i64.store8 offset=132
                  local.get 1
                  local.get 4
                  i64.const 24
                  i64.shr_u
                  i64.store8 offset=131
                  local.get 1
                  local.get 4
                  i64.const 16
                  i64.shr_u
                  i64.store8 offset=130
                  local.get 1
                  local.get 4
                  i64.const 8
                  i64.shr_u
                  i64.store8 offset=129
                  local.get 1
                  local.get 4
                  i64.store8 offset=128
                  local.get 1
                  i32.const 128
                  i32.add
                  local.get 1
                  i32.const 96
                  i32.add
                  call 2
                  local.get 1
                  i32.const 32
                  i32.add
                  i32.const 8
                  i32.add
                  local.get 9
                  i64.load
                  i64.store
                  local.get 1
                  i32.const 32
                  i32.add
                  i32.const 16
                  i32.add
                  local.get 8
                  i64.load
                  i64.store
                  local.get 1
                  i32.const 32
                  i32.add
                  i32.const 24
                  i32.add
                  local.get 2
                  i64.load
                  i64.store
                  local.get 1
                  local.get 1
                  i64.load offset=96
                  i64.store offset=32
                  i32.const 0
                  local.set 9
                  i32.const 0
                  i32.load8_u offset=8648
                  drop
                  i32.const 32
                  local.set 8
                  i32.const 32
                  call 12
                  local.tee 2
                  i32.eqz
                  br_if 5 (;@2;)
                  local.get 2
                  local.get 1
                  i64.load offset=32
                  i64.store align=1
                  local.get 2
                  i32.const 24
                  i32.add
                  local.get 1
                  i32.const 32
                  i32.add
                  i32.const 24
                  i32.add
                  i64.load
                  i64.store align=1
                  local.get 2
                  i32.const 16
                  i32.add
                  local.get 1
                  i32.const 32
                  i32.add
                  i32.const 16
                  i32.add
                  i64.load
                  i64.store align=1
                  local.get 2
                  i32.const 8
                  i32.add
                  local.get 1
                  i32.const 32
                  i32.add
                  i32.const 8
                  i32.add
                  i64.load
                  i64.store align=1
                  br 4 (;@3;)
                end
                local.get 1
                i32.const 96
                i32.add
                i32.const 24
                i32.add
                local.tee 2
                i64.const 0
                i64.store
                local.get 1
                i32.const 96
                i32.add
                i32.const 16
                i32.add
                local.tee 8
                i64.const 0
                i64.store
                local.get 1
                i32.const 96
                i32.add
                i32.const 8
                i32.add
                local.tee 9
                i64.const 0
                i64.store
                local.get 1
                i64.const 0
                i64.store offset=96
                local.get 1
                local.get 1
                i32.const 96
                i32.add
                call 3
                local.get 1
                i32.const 128
                i32.add
                i32.const 24
                i32.add
                local.get 2
                i64.load
                i64.store
                local.get 1
                i32.const 128
                i32.add
                i32.const 16
                i32.add
                local.get 8
                i64.load
                i64.store
                local.get 1
                i32.const 128
                i32.add
                i32.const 8
                i32.add
                local.get 9
                i64.load
                i64.store
                i32.const 0
                local.set 9
                i32.const 0
                i32.load8_u offset=8648
                drop
                local.get 1
                local.get 1
                i64.load offset=96
                i64.store offset=128
                i32.const 32
                local.set 8
                i32.const 32
                call 12
                local.tee 2
                i32.eqz
                br_if 4 (;@2;)
                local.get 2
                local.get 1
                i64.load offset=128
                i64.store align=1
                local.get 2
                i32.const 24
                i32.add
                local.get 1
                i32.const 128
                i32.add
                i32.const 24
                i32.add
                i64.load
                i64.store align=1
                local.get 2
                i32.const 16
                i32.add
                local.get 1
                i32.const 128
                i32.add
                i32.const 16
                i32.add
                i64.load
                i64.store align=1
                local.get 2
                i32.const 8
                i32.add
                local.get 1
                i32.const 128
                i32.add
                i32.const 8
                i32.add
                i64.load
                i64.store align=1
                br 3 (;@3;)
              end
              local.get 0
              i32.const 65
              i32.ne
              br_if 3 (;@2;)
              local.get 1
              i32.const 88
              i32.add
              local.get 3
              i32.const 57
              i32.add
              i64.load align=1
              i64.store
              local.get 1
              i32.const 80
              i32.add
              local.get 3
              i32.const 49
              i32.add
              i64.load align=1
              i64.store
              local.get 1
              i32.const 72
              i32.add
              local.get 3
              i32.const 41
              i32.add
              i64.load align=1
              i64.store
              local.get 1
              local.get 3
              i64.load offset=33 align=1
              i64.store offset=64
              local.get 1
              i64.load
              local.set 4
              local.get 1
              i64.load offset=8
              local.set 5
              local.get 1
              i64.load offset=16
              local.set 6
              local.get 1
              local.get 1
              i64.load offset=24
              local.tee 7
              i64.const 56
              i64.shr_u
              i64.store8 offset=159
              local.get 1
              local.get 7
              i64.const 48
              i64.shr_u
              i64.store8 offset=158
              local.get 1
              local.get 7
              i64.const 40
              i64.shr_u
              i64.store8 offset=157
              local.get 1
              local.get 7
              i64.const 32
              i64.shr_u
              i64.store8 offset=156
              local.get 1
              local.get 7
              i64.const 24
              i64.shr_u
              i64.store8 offset=155
              local.get 1
              local.get 7
              i64.const 16
              i64.shr_u
              i64.store8 offset=154
              local.get 1
              local.get 7
              i64.const 8
              i64.shr_u
              i64.store8 offset=153
              local.get 1
              local.get 7
              i64.store8 offset=152
              local.get 1
              local.get 6
              i64.const 56
              i64.shr_u
              i64.store8 offset=151
              local.get 1
              local.get 6
              i64.const 48
              i64.shr_u
              i64.store8 offset=150
              local.get 1
              local.get 6
              i64.const 40
              i64.shr_u
              i64.store8 offset=149
              local.get 1
              local.get 6
              i64.const 32
              i64.shr_u
              i64.store8 offset=148
              local.get 1
              local.get 6
              i64.const 24
              i64.shr_u
              i64.store8 offset=147
              local.get 1
              local.get 6
              i64.const 16
              i64.shr_u
              i64.store8 offset=146
              local.get 1
              local.get 6
              i64.const 8
              i64.shr_u
              i64.store8 offset=145
              local.get 1
              local.get 6
              i64.store8 offset=144
              local.get 1
              local.get 5
              i64.const 56
              i64.shr_u
              i64.store8 offset=143
              local.get 1
              local.get 5
              i64.const 48
              i64.shr_u
              i64.store8 offset=142
              local.get 1
              local.get 5
              i64.const 40
              i64.shr_u
              i64.store8 offset=141
              local.get 1
              local.get 5
              i64.const 32
              i64.shr_u
              i64.store8 offset=140
              local.get 1
              local.get 5
              i64.const 24
              i64.shr_u
              i64.store8 offset=139
              local.get 1
              local.get 5
              i64.const 16
              i64.shr_u
              i64.store8 offset=138
              local.get 1
              local.get 5
              i64.const 8
              i64.shr_u
              i64.store8 offset=137
              local.get 1
              local.get 5
              i64.store8 offset=136
              local.get 1
              local.get 4
              i64.const 56
              i64.shr_u
              i64.store8 offset=135
              local.get 1
              local.get 4
              i64.const 48
              i64.shr_u
              i64.store8 offset=134
              local.get 1
              local.get 4
              i64.const 40
              i64.shr_u
              i64.store8 offset=133
              local.get 1
              local.get 4
              i64.const 32
              i64.shr_u
              i64.store8 offset=132
              local.get 1
              local.get 4
              i64.const 24
              i64.shr_u
              i64.store8 offset=131
              local.get 1
              local.get 4
              i64.const 16
              i64.shr_u
              i64.store8 offset=130
              local.get 1
              local.get 4
              i64.const 8
              i64.shr_u
              i64.store8 offset=129
              local.get 1
              local.get 4
              i64.store8 offset=128
              local.get 1
              i32.const 128
              i32.add
              local.get 1
              i32.const 64
              i32.add
              call 4
              br 1 (;@4;)
            end
            local.get 0
            i32.const 65
            i32.ne
            br_if 2 (;@2;)
            local.get 1
            i32.const 152
            i32.add
            local.get 3
            i32.const 57
            i32.add
            i64.load align=1
            i64.store
            local.get 1
            i32.const 144
            i32.add
            local.get 3
            i32.const 49
            i32.add
            i64.load align=1
            i64.store
            local.get 1
            i32.const 136
            i32.add
            local.get 3
            i32.const 41
            i32.add
            i64.load align=1
            i64.store
            local.get 1
            local.get 3
            i64.load offset=33 align=1
            i64.store offset=128
            local.get 1
            local.get 1
            i32.const 128
            i32.add
            call 5
          end
          i32.const 1
          local.set 9
          i32.const 0
          local.set 8
          i32.const 1
          local.set 2
        end
        local.get 3
        local.get 0
        call 13
        i32.const 0
        local.set 3
        i32.const 0
        call 6
        local.get 2
        local.get 8
        call 7
        local.get 9
        br_if 1 (;@1;)
        local.get 2
        local.get 8
        call 13
        br 1 (;@1;)
      end
      unreachable
    end
    local.get 1
    i32.const 160
    i32.add
    global.set 0
    local.get 3)
  (func (;12;) (type 4) (param i32) (result i32)
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
                    i32.const 245
                    i32.lt_u
                    br_if 0 (;@8;)
                    local.get 0
                    i32.const 11
                    i32.add
                    local.tee 1
                    i32.const -8
                    i32.and
                    local.set 2
                    i32.const 0
                    i32.load offset=8608
                    local.tee 3
                    i32.eqz
                    br_if 4 (;@4;)
                    i32.const 31
                    local.set 4
                    block  ;; label = @9
                      local.get 0
                      i32.const 16777204
                      i32.gt_u
                      br_if 0 (;@9;)
                      local.get 2
                      i32.const 6
                      local.get 1
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
                      local.set 4
                    end
                    i32.const 0
                    local.get 2
                    i32.sub
                    local.set 1
                    block  ;; label = @9
                      local.get 4
                      i32.const 2
                      i32.shl
                      i32.const 8196
                      i32.add
                      i32.load
                      local.tee 5
                      br_if 0 (;@9;)
                      i32.const 0
                      local.set 0
                      i32.const 0
                      local.set 6
                      br 2 (;@7;)
                    end
                    i32.const 0
                    local.set 0
                    local.get 2
                    i32.const 0
                    i32.const 25
                    local.get 4
                    i32.const 1
                    i32.shr_u
                    i32.sub
                    local.get 4
                    i32.const 31
                    i32.eq
                    select
                    i32.shl
                    local.set 7
                    i32.const 0
                    local.set 6
                    loop  ;; label = @9
                      block  ;; label = @10
                        local.get 5
                        local.tee 5
                        i32.load offset=4
                        i32.const -8
                        i32.and
                        local.tee 8
                        local.get 2
                        i32.lt_u
                        br_if 0 (;@10;)
                        local.get 8
                        local.get 2
                        i32.sub
                        local.tee 8
                        local.get 1
                        i32.ge_u
                        br_if 0 (;@10;)
                        local.get 8
                        local.set 1
                        local.get 5
                        local.set 6
                        local.get 8
                        br_if 0 (;@10;)
                        i32.const 0
                        local.set 1
                        local.get 5
                        local.set 6
                        local.get 5
                        local.set 0
                        br 4 (;@6;)
                      end
                      local.get 5
                      i32.load offset=20
                      local.tee 8
                      local.get 0
                      local.get 8
                      local.get 5
                      local.get 7
                      i32.const 29
                      i32.shr_u
                      i32.const 4
                      i32.and
                      i32.add
                      i32.load offset=16
                      local.tee 5
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
                      local.get 5
                      i32.eqz
                      br_if 2 (;@7;)
                      br 0 (;@9;)
                    end
                  end
                  block  ;; label = @8
                    i32.const 0
                    i32.load offset=8604
                    local.tee 5
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
                    local.tee 1
                    i32.shr_u
                    local.tee 0
                    i32.const 3
                    i32.and
                    i32.eqz
                    br_if 0 (;@8;)
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 0
                        i32.const -1
                        i32.xor
                        i32.const 1
                        i32.and
                        local.get 1
                        i32.add
                        local.tee 7
                        i32.const 3
                        i32.shl
                        local.tee 0
                        i32.const 8340
                        i32.add
                        local.tee 2
                        local.get 0
                        i32.const 8348
                        i32.add
                        i32.load
                        local.tee 1
                        i32.load offset=8
                        local.tee 6
                        i32.eq
                        br_if 0 (;@10;)
                        local.get 6
                        local.get 2
                        i32.store offset=12
                        local.get 2
                        local.get 6
                        i32.store offset=8
                        br 1 (;@9;)
                      end
                      i32.const 0
                      local.get 5
                      i32.const -2
                      local.get 7
                      i32.rotl
                      i32.and
                      i32.store offset=8604
                    end
                    local.get 1
                    i32.const 8
                    i32.add
                    local.set 6
                    local.get 1
                    local.get 0
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
                    br 5 (;@3;)
                  end
                  local.get 2
                  i32.const 0
                  i32.load offset=8612
                  i32.le_u
                  br_if 3 (;@4;)
                  block  ;; label = @8
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 0
                        br_if 0 (;@10;)
                        i32.const 0
                        i32.load offset=8608
                        local.tee 0
                        i32.eqz
                        br_if 6 (;@4;)
                        local.get 0
                        i32.ctz
                        i32.const 2
                        i32.shl
                        i32.const 8196
                        i32.add
                        i32.load
                        local.tee 6
                        i32.load offset=4
                        i32.const -8
                        i32.and
                        local.get 2
                        i32.sub
                        local.set 1
                        local.get 6
                        local.set 5
                        loop  ;; label = @11
                          block  ;; label = @12
                            local.get 6
                            i32.load offset=16
                            local.tee 0
                            br_if 0 (;@12;)
                            local.get 6
                            i32.load offset=20
                            local.tee 0
                            br_if 0 (;@12;)
                            local.get 5
                            i32.load offset=24
                            local.set 4
                            block  ;; label = @13
                              block  ;; label = @14
                                block  ;; label = @15
                                  local.get 5
                                  i32.load offset=12
                                  local.tee 0
                                  local.get 5
                                  i32.ne
                                  br_if 0 (;@15;)
                                  local.get 5
                                  i32.const 20
                                  i32.const 16
                                  local.get 5
                                  i32.load offset=20
                                  local.tee 0
                                  select
                                  i32.add
                                  i32.load
                                  local.tee 6
                                  br_if 1 (;@14;)
                                  i32.const 0
                                  local.set 0
                                  br 2 (;@13;)
                                end
                                local.get 5
                                i32.load offset=8
                                local.tee 6
                                local.get 0
                                i32.store offset=12
                                local.get 0
                                local.get 6
                                i32.store offset=8
                                br 1 (;@13;)
                              end
                              local.get 5
                              i32.const 20
                              i32.add
                              local.get 5
                              i32.const 16
                              i32.add
                              local.get 0
                              select
                              local.set 7
                              loop  ;; label = @14
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
                                br_if 0 (;@14;)
                              end
                              local.get 8
                              i32.const 0
                              i32.store
                            end
                            local.get 4
                            i32.eqz
                            br_if 4 (;@8;)
                            block  ;; label = @13
                              block  ;; label = @14
                                local.get 5
                                local.get 5
                                i32.load offset=28
                                i32.const 2
                                i32.shl
                                i32.const 8196
                                i32.add
                                local.tee 6
                                i32.load
                                i32.eq
                                br_if 0 (;@14;)
                                block  ;; label = @15
                                  local.get 4
                                  i32.load offset=16
                                  local.get 5
                                  i32.eq
                                  br_if 0 (;@15;)
                                  local.get 4
                                  local.get 0
                                  i32.store offset=20
                                  local.get 0
                                  br_if 2 (;@13;)
                                  br 7 (;@8;)
                                end
                                local.get 4
                                local.get 0
                                i32.store offset=16
                                local.get 0
                                br_if 1 (;@13;)
                                br 6 (;@8;)
                              end
                              local.get 6
                              local.get 0
                              i32.store
                              local.get 0
                              i32.eqz
                              br_if 4 (;@9;)
                            end
                            local.get 0
                            local.get 4
                            i32.store offset=24
                            block  ;; label = @13
                              local.get 5
                              i32.load offset=16
                              local.tee 6
                              i32.eqz
                              br_if 0 (;@13;)
                              local.get 0
                              local.get 6
                              i32.store offset=16
                              local.get 6
                              local.get 0
                              i32.store offset=24
                            end
                            local.get 5
                            i32.load offset=20
                            local.tee 6
                            i32.eqz
                            br_if 4 (;@8;)
                            local.get 0
                            local.get 6
                            i32.store offset=20
                            local.get 6
                            local.get 0
                            i32.store offset=24
                            br 4 (;@8;)
                          end
                          local.get 0
                          i32.load offset=4
                          i32.const -8
                          i32.and
                          local.get 2
                          i32.sub
                          local.tee 6
                          local.get 1
                          local.get 6
                          local.get 1
                          i32.lt_u
                          local.tee 6
                          select
                          local.set 1
                          local.get 0
                          local.get 5
                          local.get 6
                          select
                          local.set 5
                          local.get 0
                          local.set 6
                          br 0 (;@11;)
                        end
                      end
                      block  ;; label = @10
                        block  ;; label = @11
                          local.get 0
                          local.get 1
                          i32.shl
                          i32.const 2
                          local.get 1
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
                          local.tee 1
                          i32.const 8340
                          i32.add
                          local.tee 6
                          local.get 1
                          i32.const 8348
                          i32.add
                          i32.load
                          local.tee 0
                          i32.load offset=8
                          local.tee 7
                          i32.eq
                          br_if 0 (;@11;)
                          local.get 7
                          local.get 6
                          i32.store offset=12
                          local.get 6
                          local.get 7
                          i32.store offset=8
                          br 1 (;@10;)
                        end
                        i32.const 0
                        local.get 5
                        i32.const -2
                        local.get 8
                        i32.rotl
                        i32.and
                        i32.store offset=8604
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
                      local.get 1
                      local.get 2
                      i32.sub
                      local.tee 2
                      i32.const 1
                      i32.or
                      i32.store offset=4
                      local.get 0
                      local.get 1
                      i32.add
                      local.get 2
                      i32.store
                      block  ;; label = @10
                        i32.const 0
                        i32.load offset=8612
                        local.tee 5
                        i32.eqz
                        br_if 0 (;@10;)
                        local.get 5
                        i32.const -8
                        i32.and
                        i32.const 8340
                        i32.add
                        local.set 6
                        i32.const 0
                        i32.load offset=8620
                        local.set 1
                        block  ;; label = @11
                          block  ;; label = @12
                            i32.const 0
                            i32.load offset=8604
                            local.tee 8
                            i32.const 1
                            local.get 5
                            i32.const 3
                            i32.shr_u
                            i32.shl
                            local.tee 5
                            i32.and
                            br_if 0 (;@12;)
                            i32.const 0
                            local.get 8
                            local.get 5
                            i32.or
                            i32.store offset=8604
                            local.get 6
                            local.set 5
                            br 1 (;@11;)
                          end
                          local.get 6
                          i32.load offset=8
                          local.set 5
                        end
                        local.get 6
                        local.get 1
                        i32.store offset=8
                        local.get 5
                        local.get 1
                        i32.store offset=12
                        local.get 1
                        local.get 6
                        i32.store offset=12
                        local.get 1
                        local.get 5
                        i32.store offset=8
                      end
                      i32.const 0
                      local.get 7
                      i32.store offset=8620
                      i32.const 0
                      local.get 2
                      i32.store offset=8612
                      local.get 0
                      i32.const 8
                      i32.add
                      return
                    end
                    i32.const 0
                    i32.const 0
                    i32.load offset=8608
                    i32.const -2
                    local.get 5
                    i32.load offset=28
                    i32.rotl
                    i32.and
                    i32.store offset=8608
                  end
                  block  ;; label = @8
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 1
                        i32.const 16
                        i32.lt_u
                        br_if 0 (;@10;)
                        local.get 5
                        local.get 2
                        i32.const 3
                        i32.or
                        i32.store offset=4
                        local.get 5
                        local.get 2
                        i32.add
                        local.tee 2
                        local.get 1
                        i32.const 1
                        i32.or
                        i32.store offset=4
                        local.get 2
                        local.get 1
                        i32.add
                        local.get 1
                        i32.store
                        i32.const 0
                        i32.load offset=8612
                        local.tee 7
                        i32.eqz
                        br_if 1 (;@9;)
                        local.get 7
                        i32.const -8
                        i32.and
                        i32.const 8340
                        i32.add
                        local.set 6
                        i32.const 0
                        i32.load offset=8620
                        local.set 0
                        block  ;; label = @11
                          block  ;; label = @12
                            i32.const 0
                            i32.load offset=8604
                            local.tee 8
                            i32.const 1
                            local.get 7
                            i32.const 3
                            i32.shr_u
                            i32.shl
                            local.tee 7
                            i32.and
                            br_if 0 (;@12;)
                            i32.const 0
                            local.get 8
                            local.get 7
                            i32.or
                            i32.store offset=8604
                            local.get 6
                            local.set 7
                            br 1 (;@11;)
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
                        br 1 (;@9;)
                      end
                      local.get 5
                      local.get 1
                      local.get 2
                      i32.add
                      local.tee 0
                      i32.const 3
                      i32.or
                      i32.store offset=4
                      local.get 5
                      local.get 0
                      i32.add
                      local.tee 0
                      local.get 0
                      i32.load offset=4
                      i32.const 1
                      i32.or
                      i32.store offset=4
                      br 1 (;@8;)
                    end
                    i32.const 0
                    local.get 2
                    i32.store offset=8620
                    i32.const 0
                    local.get 1
                    i32.store offset=8612
                  end
                  local.get 5
                  i32.const 8
                  i32.add
                  return
                end
                block  ;; label = @7
                  local.get 0
                  local.get 6
                  i32.or
                  br_if 0 (;@7;)
                  i32.const 0
                  local.set 6
                  i32.const 2
                  local.get 4
                  i32.shl
                  local.tee 0
                  i32.const 0
                  local.get 0
                  i32.sub
                  i32.or
                  local.get 3
                  i32.and
                  local.tee 0
                  i32.eqz
                  br_if 3 (;@4;)
                  local.get 0
                  i32.ctz
                  i32.const 2
                  i32.shl
                  i32.const 8196
                  i32.add
                  i32.load
                  local.set 0
                end
                local.get 0
                i32.eqz
                br_if 1 (;@5;)
              end
              loop  ;; label = @6
                local.get 0
                local.get 6
                local.get 0
                i32.load offset=4
                i32.const -8
                i32.and
                local.tee 5
                local.get 2
                i32.sub
                local.tee 8
                local.get 1
                i32.lt_u
                local.tee 4
                select
                local.set 3
                local.get 5
                local.get 2
                i32.lt_u
                local.set 7
                local.get 8
                local.get 1
                local.get 4
                select
                local.set 8
                block  ;; label = @7
                  local.get 0
                  i32.load offset=16
                  local.tee 5
                  br_if 0 (;@7;)
                  local.get 0
                  i32.load offset=20
                  local.set 5
                end
                local.get 6
                local.get 3
                local.get 7
                select
                local.set 6
                local.get 1
                local.get 8
                local.get 7
                select
                local.set 1
                local.get 5
                local.set 0
                local.get 5
                br_if 0 (;@6;)
              end
            end
            local.get 6
            i32.eqz
            br_if 0 (;@4;)
            block  ;; label = @5
              i32.const 0
              i32.load offset=8612
              local.tee 0
              local.get 2
              i32.lt_u
              br_if 0 (;@5;)
              local.get 1
              local.get 0
              local.get 2
              i32.sub
              i32.ge_u
              br_if 1 (;@4;)
            end
            local.get 6
            i32.load offset=24
            local.set 4
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  local.get 6
                  i32.load offset=12
                  local.tee 0
                  local.get 6
                  i32.ne
                  br_if 0 (;@7;)
                  local.get 6
                  i32.const 20
                  i32.const 16
                  local.get 6
                  i32.load offset=20
                  local.tee 0
                  select
                  i32.add
                  i32.load
                  local.tee 5
                  br_if 1 (;@6;)
                  i32.const 0
                  local.set 0
                  br 2 (;@5;)
                end
                local.get 6
                i32.load offset=8
                local.tee 5
                local.get 0
                i32.store offset=12
                local.get 0
                local.get 5
                i32.store offset=8
                br 1 (;@5;)
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
              loop  ;; label = @6
                local.get 7
                local.set 8
                local.get 5
                local.tee 0
                i32.const 20
                i32.add
                local.get 0
                i32.const 16
                i32.add
                local.get 0
                i32.load offset=20
                local.tee 5
                select
                local.set 7
                local.get 0
                i32.const 20
                i32.const 16
                local.get 5
                select
                i32.add
                i32.load
                local.tee 5
                br_if 0 (;@6;)
              end
              local.get 8
              i32.const 0
              i32.store
            end
            local.get 4
            i32.eqz
            br_if 3 (;@1;)
            block  ;; label = @5
              block  ;; label = @6
                local.get 6
                local.get 6
                i32.load offset=28
                i32.const 2
                i32.shl
                i32.const 8196
                i32.add
                local.tee 5
                i32.load
                i32.eq
                br_if 0 (;@6;)
                block  ;; label = @7
                  local.get 4
                  i32.load offset=16
                  local.get 6
                  i32.eq
                  br_if 0 (;@7;)
                  local.get 4
                  local.get 0
                  i32.store offset=20
                  local.get 0
                  br_if 2 (;@5;)
                  br 6 (;@1;)
                end
                local.get 4
                local.get 0
                i32.store offset=16
                local.get 0
                br_if 1 (;@5;)
                br 5 (;@1;)
              end
              local.get 5
              local.get 0
              i32.store
              local.get 0
              i32.eqz
              br_if 3 (;@2;)
            end
            local.get 0
            local.get 4
            i32.store offset=24
            block  ;; label = @5
              local.get 6
              i32.load offset=16
              local.tee 5
              i32.eqz
              br_if 0 (;@5;)
              local.get 0
              local.get 5
              i32.store offset=16
              local.get 5
              local.get 0
              i32.store offset=24
            end
            local.get 6
            i32.load offset=20
            local.tee 5
            i32.eqz
            br_if 3 (;@1;)
            local.get 0
            local.get 5
            i32.store offset=20
            local.get 5
            local.get 0
            i32.store offset=24
            br 3 (;@1;)
          end
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  block  ;; label = @8
                    block  ;; label = @9
                      i32.const 0
                      i32.load offset=8612
                      local.tee 0
                      local.get 2
                      i32.ge_u
                      br_if 0 (;@9;)
                      block  ;; label = @10
                        i32.const 0
                        i32.load offset=8616
                        local.tee 0
                        local.get 2
                        i32.gt_u
                        br_if 0 (;@10;)
                        i32.const 0
                        local.set 6
                        local.get 2
                        i32.const 65583
                        i32.add
                        local.tee 1
                        i32.const 16
                        i32.shr_u
                        memory.grow
                        local.tee 0
                        i32.const -1
                        i32.eq
                        local.tee 7
                        br_if 7 (;@3;)
                        local.get 0
                        i32.const 16
                        i32.shl
                        local.tee 5
                        i32.eqz
                        br_if 7 (;@3;)
                        i32.const 0
                        i32.const 0
                        i32.load offset=8628
                        i32.const 0
                        local.get 1
                        i32.const -65536
                        i32.and
                        local.get 7
                        select
                        local.tee 8
                        i32.add
                        local.tee 0
                        i32.store offset=8628
                        i32.const 0
                        local.get 0
                        i32.const 0
                        i32.load offset=8632
                        local.tee 1
                        local.get 0
                        local.get 1
                        i32.gt_u
                        select
                        i32.store offset=8632
                        block  ;; label = @11
                          block  ;; label = @12
                            block  ;; label = @13
                              i32.const 0
                              i32.load offset=8624
                              local.tee 1
                              i32.eqz
                              br_if 0 (;@13;)
                              i32.const 8324
                              local.set 0
                              loop  ;; label = @14
                                local.get 0
                                i32.load
                                local.tee 6
                                local.get 0
                                i32.load offset=4
                                local.tee 7
                                i32.add
                                local.get 5
                                i32.eq
                                br_if 2 (;@12;)
                                local.get 0
                                i32.load offset=8
                                local.tee 0
                                br_if 0 (;@14;)
                                br 3 (;@11;)
                              end
                            end
                            block  ;; label = @13
                              block  ;; label = @14
                                i32.const 0
                                i32.load offset=8640
                                local.tee 0
                                i32.eqz
                                br_if 0 (;@14;)
                                local.get 0
                                local.get 5
                                i32.le_u
                                br_if 1 (;@13;)
                              end
                              i32.const 0
                              local.get 5
                              i32.store offset=8640
                            end
                            i32.const 0
                            i32.const 4095
                            i32.store offset=8644
                            i32.const 0
                            local.get 8
                            i32.store offset=8328
                            i32.const 0
                            local.get 5
                            i32.store offset=8324
                            i32.const 0
                            i32.const 8340
                            i32.store offset=8352
                            i32.const 0
                            i32.const 8348
                            i32.store offset=8360
                            i32.const 0
                            i32.const 8340
                            i32.store offset=8348
                            i32.const 0
                            i32.const 8356
                            i32.store offset=8368
                            i32.const 0
                            i32.const 8348
                            i32.store offset=8356
                            i32.const 0
                            i32.const 8364
                            i32.store offset=8376
                            i32.const 0
                            i32.const 8356
                            i32.store offset=8364
                            i32.const 0
                            i32.const 8372
                            i32.store offset=8384
                            i32.const 0
                            i32.const 8364
                            i32.store offset=8372
                            i32.const 0
                            i32.const 8380
                            i32.store offset=8392
                            i32.const 0
                            i32.const 8372
                            i32.store offset=8380
                            i32.const 0
                            i32.const 8388
                            i32.store offset=8400
                            i32.const 0
                            i32.const 8380
                            i32.store offset=8388
                            i32.const 0
                            i32.const 8396
                            i32.store offset=8408
                            i32.const 0
                            i32.const 8388
                            i32.store offset=8396
                            i32.const 0
                            i32.const 0
                            i32.store offset=8336
                            i32.const 0
                            i32.const 8404
                            i32.store offset=8416
                            i32.const 0
                            i32.const 8396
                            i32.store offset=8404
                            i32.const 0
                            i32.const 8404
                            i32.store offset=8412
                            i32.const 0
                            i32.const 8412
                            i32.store offset=8424
                            i32.const 0
                            i32.const 8412
                            i32.store offset=8420
                            i32.const 0
                            i32.const 8420
                            i32.store offset=8432
                            i32.const 0
                            i32.const 8420
                            i32.store offset=8428
                            i32.const 0
                            i32.const 8428
                            i32.store offset=8440
                            i32.const 0
                            i32.const 8428
                            i32.store offset=8436
                            i32.const 0
                            i32.const 8436
                            i32.store offset=8448
                            i32.const 0
                            i32.const 8436
                            i32.store offset=8444
                            i32.const 0
                            i32.const 8444
                            i32.store offset=8456
                            i32.const 0
                            i32.const 8444
                            i32.store offset=8452
                            i32.const 0
                            i32.const 8452
                            i32.store offset=8464
                            i32.const 0
                            i32.const 8452
                            i32.store offset=8460
                            i32.const 0
                            i32.const 8460
                            i32.store offset=8472
                            i32.const 0
                            i32.const 8460
                            i32.store offset=8468
                            i32.const 0
                            i32.const 8468
                            i32.store offset=8480
                            i32.const 0
                            i32.const 8476
                            i32.store offset=8488
                            i32.const 0
                            i32.const 8468
                            i32.store offset=8476
                            i32.const 0
                            i32.const 8484
                            i32.store offset=8496
                            i32.const 0
                            i32.const 8476
                            i32.store offset=8484
                            i32.const 0
                            i32.const 8492
                            i32.store offset=8504
                            i32.const 0
                            i32.const 8484
                            i32.store offset=8492
                            i32.const 0
                            i32.const 8500
                            i32.store offset=8512
                            i32.const 0
                            i32.const 8492
                            i32.store offset=8500
                            i32.const 0
                            i32.const 8508
                            i32.store offset=8520
                            i32.const 0
                            i32.const 8500
                            i32.store offset=8508
                            i32.const 0
                            i32.const 8516
                            i32.store offset=8528
                            i32.const 0
                            i32.const 8508
                            i32.store offset=8516
                            i32.const 0
                            i32.const 8524
                            i32.store offset=8536
                            i32.const 0
                            i32.const 8516
                            i32.store offset=8524
                            i32.const 0
                            i32.const 8532
                            i32.store offset=8544
                            i32.const 0
                            i32.const 8524
                            i32.store offset=8532
                            i32.const 0
                            i32.const 8540
                            i32.store offset=8552
                            i32.const 0
                            i32.const 8532
                            i32.store offset=8540
                            i32.const 0
                            i32.const 8548
                            i32.store offset=8560
                            i32.const 0
                            i32.const 8540
                            i32.store offset=8548
                            i32.const 0
                            i32.const 8556
                            i32.store offset=8568
                            i32.const 0
                            i32.const 8548
                            i32.store offset=8556
                            i32.const 0
                            i32.const 8564
                            i32.store offset=8576
                            i32.const 0
                            i32.const 8556
                            i32.store offset=8564
                            i32.const 0
                            i32.const 8572
                            i32.store offset=8584
                            i32.const 0
                            i32.const 8564
                            i32.store offset=8572
                            i32.const 0
                            i32.const 8580
                            i32.store offset=8592
                            i32.const 0
                            i32.const 8572
                            i32.store offset=8580
                            i32.const 0
                            i32.const 8588
                            i32.store offset=8600
                            i32.const 0
                            i32.const 8580
                            i32.store offset=8588
                            i32.const 0
                            local.get 5
                            i32.store offset=8624
                            i32.const 0
                            i32.const 8588
                            i32.store offset=8596
                            i32.const 0
                            local.get 8
                            i32.const -40
                            i32.add
                            local.tee 0
                            i32.store offset=8616
                            local.get 5
                            local.get 0
                            i32.const 1
                            i32.or
                            i32.store offset=4
                            local.get 5
                            local.get 0
                            i32.add
                            i32.const 40
                            i32.store offset=4
                            i32.const 0
                            i32.const 2097152
                            i32.store offset=8636
                            br 8 (;@4;)
                          end
                          local.get 1
                          local.get 5
                          i32.ge_u
                          br_if 0 (;@11;)
                          local.get 6
                          local.get 1
                          i32.gt_u
                          br_if 0 (;@11;)
                          local.get 0
                          i32.load offset=12
                          i32.eqz
                          br_if 3 (;@8;)
                        end
                        i32.const 0
                        i32.const 0
                        i32.load offset=8640
                        local.tee 0
                        local.get 5
                        local.get 0
                        local.get 5
                        i32.lt_u
                        select
                        i32.store offset=8640
                        local.get 5
                        local.get 8
                        i32.add
                        local.set 6
                        i32.const 8324
                        local.set 0
                        block  ;; label = @11
                          block  ;; label = @12
                            block  ;; label = @13
                              loop  ;; label = @14
                                local.get 0
                                i32.load
                                local.tee 7
                                local.get 6
                                i32.eq
                                br_if 1 (;@13;)
                                local.get 0
                                i32.load offset=8
                                local.tee 0
                                br_if 0 (;@14;)
                                br 2 (;@12;)
                              end
                            end
                            local.get 0
                            i32.load offset=12
                            i32.eqz
                            br_if 1 (;@11;)
                          end
                          i32.const 8324
                          local.set 0
                          block  ;; label = @12
                            loop  ;; label = @13
                              block  ;; label = @14
                                local.get 0
                                i32.load
                                local.tee 6
                                local.get 1
                                i32.gt_u
                                br_if 0 (;@14;)
                                local.get 1
                                local.get 6
                                local.get 0
                                i32.load offset=4
                                i32.add
                                local.tee 6
                                i32.lt_u
                                br_if 2 (;@12;)
                              end
                              local.get 0
                              i32.load offset=8
                              local.set 0
                              br 0 (;@13;)
                            end
                          end
                          i32.const 0
                          local.get 5
                          i32.store offset=8624
                          i32.const 0
                          local.get 8
                          i32.const -40
                          i32.add
                          local.tee 0
                          i32.store offset=8616
                          local.get 5
                          local.get 0
                          i32.const 1
                          i32.or
                          i32.store offset=4
                          local.get 5
                          local.get 0
                          i32.add
                          i32.const 40
                          i32.store offset=4
                          i32.const 0
                          i32.const 2097152
                          i32.store offset=8636
                          local.get 1
                          local.get 6
                          i32.const -32
                          i32.add
                          i32.const -8
                          i32.and
                          i32.const -8
                          i32.add
                          local.tee 0
                          local.get 0
                          local.get 1
                          i32.const 16
                          i32.add
                          i32.lt_u
                          select
                          local.tee 7
                          i32.const 27
                          i32.store offset=4
                          i32.const 0
                          i64.load offset=8324 align=4
                          local.set 9
                          local.get 7
                          i32.const 16
                          i32.add
                          i32.const 0
                          i64.load offset=8332 align=4
                          i64.store align=4
                          local.get 7
                          local.get 9
                          i64.store offset=8 align=4
                          i32.const 0
                          local.get 8
                          i32.store offset=8328
                          i32.const 0
                          local.get 5
                          i32.store offset=8324
                          i32.const 0
                          local.get 7
                          i32.const 8
                          i32.add
                          i32.store offset=8332
                          i32.const 0
                          i32.const 0
                          i32.store offset=8336
                          local.get 7
                          i32.const 28
                          i32.add
                          local.set 0
                          loop  ;; label = @12
                            local.get 0
                            i32.const 7
                            i32.store
                            local.get 0
                            i32.const 4
                            i32.add
                            local.tee 0
                            local.get 6
                            i32.lt_u
                            br_if 0 (;@12;)
                          end
                          local.get 7
                          local.get 1
                          i32.eq
                          br_if 7 (;@4;)
                          local.get 7
                          local.get 7
                          i32.load offset=4
                          i32.const -2
                          i32.and
                          i32.store offset=4
                          local.get 1
                          local.get 7
                          local.get 1
                          i32.sub
                          local.tee 0
                          i32.const 1
                          i32.or
                          i32.store offset=4
                          local.get 7
                          local.get 0
                          i32.store
                          block  ;; label = @12
                            local.get 0
                            i32.const 256
                            i32.lt_u
                            br_if 0 (;@12;)
                            local.get 1
                            local.get 0
                            call 14
                            br 8 (;@4;)
                          end
                          local.get 0
                          i32.const 248
                          i32.and
                          i32.const 8340
                          i32.add
                          local.set 6
                          block  ;; label = @12
                            block  ;; label = @13
                              i32.const 0
                              i32.load offset=8604
                              local.tee 5
                              i32.const 1
                              local.get 0
                              i32.const 3
                              i32.shr_u
                              i32.shl
                              local.tee 0
                              i32.and
                              br_if 0 (;@13;)
                              i32.const 0
                              local.get 5
                              local.get 0
                              i32.or
                              i32.store offset=8604
                              local.get 6
                              local.set 0
                              br 1 (;@12;)
                            end
                            local.get 6
                            i32.load offset=8
                            local.set 0
                          end
                          local.get 6
                          local.get 1
                          i32.store offset=8
                          local.get 0
                          local.get 1
                          i32.store offset=12
                          local.get 1
                          local.get 6
                          i32.store offset=12
                          local.get 1
                          local.get 0
                          i32.store offset=8
                          br 7 (;@4;)
                        end
                        local.get 0
                        local.get 5
                        i32.store
                        local.get 0
                        local.get 0
                        i32.load offset=4
                        local.get 8
                        i32.add
                        i32.store offset=4
                        local.get 5
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
                        local.get 5
                        local.get 2
                        i32.add
                        local.tee 0
                        i32.sub
                        local.set 1
                        local.get 6
                        i32.const 0
                        i32.load offset=8624
                        i32.eq
                        br_if 3 (;@7;)
                        local.get 6
                        i32.const 0
                        i32.load offset=8620
                        i32.eq
                        br_if 4 (;@6;)
                        block  ;; label = @11
                          local.get 6
                          i32.load offset=4
                          local.tee 2
                          i32.const 3
                          i32.and
                          i32.const 1
                          i32.ne
                          br_if 0 (;@11;)
                          local.get 6
                          local.get 2
                          i32.const -8
                          i32.and
                          local.tee 2
                          call 15
                          local.get 2
                          local.get 1
                          i32.add
                          local.set 1
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
                        local.get 1
                        i32.const 1
                        i32.or
                        i32.store offset=4
                        local.get 0
                        local.get 1
                        i32.add
                        local.get 1
                        i32.store
                        block  ;; label = @11
                          local.get 1
                          i32.const 256
                          i32.lt_u
                          br_if 0 (;@11;)
                          local.get 0
                          local.get 1
                          call 14
                          br 6 (;@5;)
                        end
                        local.get 1
                        i32.const 248
                        i32.and
                        i32.const 8340
                        i32.add
                        local.set 2
                        block  ;; label = @11
                          block  ;; label = @12
                            i32.const 0
                            i32.load offset=8604
                            local.tee 6
                            i32.const 1
                            local.get 1
                            i32.const 3
                            i32.shr_u
                            i32.shl
                            local.tee 1
                            i32.and
                            br_if 0 (;@12;)
                            i32.const 0
                            local.get 6
                            local.get 1
                            i32.or
                            i32.store offset=8604
                            local.get 2
                            local.set 1
                            br 1 (;@11;)
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
                        br 5 (;@5;)
                      end
                      i32.const 0
                      local.get 0
                      local.get 2
                      i32.sub
                      local.tee 1
                      i32.store offset=8616
                      i32.const 0
                      i32.const 0
                      i32.load offset=8624
                      local.tee 0
                      local.get 2
                      i32.add
                      local.tee 6
                      i32.store offset=8624
                      local.get 6
                      local.get 1
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
                    i32.const 0
                    i32.load offset=8620
                    local.set 1
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 0
                        local.get 2
                        i32.sub
                        local.tee 6
                        i32.const 15
                        i32.gt_u
                        br_if 0 (;@10;)
                        i32.const 0
                        i32.const 0
                        i32.store offset=8620
                        i32.const 0
                        i32.const 0
                        i32.store offset=8612
                        local.get 1
                        local.get 0
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
                        br 1 (;@9;)
                      end
                      i32.const 0
                      local.get 6
                      i32.store offset=8612
                      i32.const 0
                      local.get 1
                      local.get 2
                      i32.add
                      local.tee 5
                      i32.store offset=8620
                      local.get 5
                      local.get 6
                      i32.const 1
                      i32.or
                      i32.store offset=4
                      local.get 1
                      local.get 0
                      i32.add
                      local.get 6
                      i32.store
                      local.get 1
                      local.get 2
                      i32.const 3
                      i32.or
                      i32.store offset=4
                    end
                    local.get 1
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
                  i32.load offset=8624
                  local.tee 0
                  i32.const 15
                  i32.add
                  i32.const -8
                  i32.and
                  local.tee 1
                  i32.const -8
                  i32.add
                  local.tee 6
                  i32.store offset=8624
                  i32.const 0
                  local.get 0
                  local.get 1
                  i32.sub
                  i32.const 0
                  i32.load offset=8616
                  local.get 8
                  i32.add
                  local.tee 1
                  i32.add
                  i32.const 8
                  i32.add
                  local.tee 5
                  i32.store offset=8616
                  local.get 6
                  local.get 5
                  i32.const 1
                  i32.or
                  i32.store offset=4
                  local.get 0
                  local.get 1
                  i32.add
                  i32.const 40
                  i32.store offset=4
                  i32.const 0
                  i32.const 2097152
                  i32.store offset=8636
                  br 3 (;@4;)
                end
                i32.const 0
                local.get 0
                i32.store offset=8624
                i32.const 0
                i32.const 0
                i32.load offset=8616
                local.get 1
                i32.add
                local.tee 1
                i32.store offset=8616
                local.get 0
                local.get 1
                i32.const 1
                i32.or
                i32.store offset=4
                br 1 (;@5;)
              end
              i32.const 0
              local.get 0
              i32.store offset=8620
              i32.const 0
              i32.const 0
              i32.load offset=8612
              local.get 1
              i32.add
              local.tee 1
              i32.store offset=8612
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
            local.get 5
            i32.const 8
            i32.add
            return
          end
          i32.const 0
          local.set 6
          i32.const 0
          i32.load offset=8616
          local.tee 0
          local.get 2
          i32.le_u
          br_if 0 (;@3;)
          i32.const 0
          local.get 0
          local.get 2
          i32.sub
          local.tee 1
          i32.store offset=8616
          i32.const 0
          i32.const 0
          i32.load offset=8624
          local.tee 0
          local.get 2
          i32.add
          local.tee 6
          i32.store offset=8624
          local.get 6
          local.get 1
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
        local.get 6
        return
      end
      i32.const 0
      i32.const 0
      i32.load offset=8608
      i32.const -2
      local.get 6
      i32.load offset=28
      i32.rotl
      i32.and
      i32.store offset=8608
    end
    block  ;; label = @1
      block  ;; label = @2
        local.get 1
        i32.const 16
        i32.lt_u
        br_if 0 (;@2;)
        local.get 6
        local.get 2
        i32.const 3
        i32.or
        i32.store offset=4
        local.get 6
        local.get 2
        i32.add
        local.tee 2
        local.get 1
        i32.const 1
        i32.or
        i32.store offset=4
        local.get 2
        local.get 1
        i32.add
        local.get 1
        i32.store
        block  ;; label = @3
          local.get 1
          i32.const 256
          i32.lt_u
          br_if 0 (;@3;)
          i32.const 31
          local.set 0
          block  ;; label = @4
            local.get 1
            i32.const 16777215
            i32.gt_u
            br_if 0 (;@4;)
            local.get 1
            i32.const 6
            local.get 1
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
          i32.const 8196
          i32.add
          local.set 5
          block  ;; label = @4
            i32.const 0
            i32.load offset=8608
            i32.const 1
            local.get 0
            i32.shl
            local.tee 7
            i32.and
            br_if 0 (;@4;)
            local.get 5
            local.get 2
            i32.store
            local.get 2
            local.get 5
            i32.store offset=24
            local.get 2
            local.get 2
            i32.store offset=12
            local.get 2
            local.get 2
            i32.store offset=8
            i32.const 0
            i32.const 0
            i32.load offset=8608
            local.get 7
            i32.or
            i32.store offset=8608
            br 3 (;@1;)
          end
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                local.get 5
                i32.load
                local.tee 7
                i32.load offset=4
                i32.const -8
                i32.and
                local.get 1
                i32.ne
                br_if 0 (;@6;)
                local.get 7
                local.set 0
                br 1 (;@5;)
              end
              local.get 1
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
              local.set 5
              loop  ;; label = @6
                local.get 7
                local.get 5
                i32.const 29
                i32.shr_u
                i32.const 4
                i32.and
                i32.add
                local.tee 8
                i32.load offset=16
                local.tee 0
                i32.eqz
                br_if 2 (;@4;)
                local.get 5
                i32.const 1
                i32.shl
                local.set 5
                local.get 0
                local.set 7
                local.get 0
                i32.load offset=4
                i32.const -8
                i32.and
                local.get 1
                i32.ne
                br_if 0 (;@6;)
              end
            end
            local.get 0
            i32.load offset=8
            local.tee 1
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
            local.get 1
            i32.store offset=8
            br 3 (;@1;)
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
          br 2 (;@1;)
        end
        local.get 1
        i32.const 248
        i32.and
        i32.const 8340
        i32.add
        local.set 0
        block  ;; label = @3
          block  ;; label = @4
            i32.const 0
            i32.load offset=8604
            local.tee 5
            i32.const 1
            local.get 1
            i32.const 3
            i32.shr_u
            i32.shl
            local.tee 1
            i32.and
            br_if 0 (;@4;)
            i32.const 0
            local.get 5
            local.get 1
            i32.or
            i32.store offset=8604
            local.get 0
            local.set 1
            br 1 (;@3;)
          end
          local.get 0
          i32.load offset=8
          local.set 1
        end
        local.get 0
        local.get 2
        i32.store offset=8
        local.get 1
        local.get 2
        i32.store offset=12
        local.get 2
        local.get 0
        i32.store offset=12
        local.get 2
        local.get 1
        i32.store offset=8
        br 1 (;@1;)
      end
      local.get 6
      local.get 1
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
    i32.add)
  (func (;13;) (type 2) (param i32 i32)
    (local i32 i32 i32 i32)
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
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
                local.tee 4
                select
                local.get 1
                i32.add
                i32.lt_u
                br_if 0 (;@6;)
                block  ;; label = @7
                  local.get 4
                  i32.eqz
                  br_if 0 (;@7;)
                  local.get 3
                  local.get 1
                  i32.const 39
                  i32.add
                  i32.gt_u
                  br_if 1 (;@6;)
                end
                local.get 0
                i32.const -8
                i32.add
                local.tee 0
                local.get 3
                i32.add
                local.set 1
                block  ;; label = @7
                  local.get 2
                  i32.const 1
                  i32.and
                  br_if 0 (;@7;)
                  local.get 2
                  i32.const 2
                  i32.and
                  i32.eqz
                  br_if 6 (;@1;)
                  local.get 0
                  i32.load
                  local.tee 2
                  local.get 3
                  i32.add
                  local.set 3
                  block  ;; label = @8
                    local.get 0
                    local.get 2
                    i32.sub
                    local.tee 0
                    i32.const 0
                    i32.load offset=8620
                    i32.ne
                    br_if 0 (;@8;)
                    local.get 1
                    i32.load offset=4
                    i32.const 3
                    i32.and
                    i32.const 3
                    i32.ne
                    br_if 1 (;@7;)
                    i32.const 0
                    local.get 3
                    i32.store offset=8612
                    local.get 1
                    local.get 1
                    i32.load offset=4
                    i32.const -2
                    i32.and
                    i32.store offset=4
                    local.get 0
                    local.get 3
                    i32.const 1
                    i32.or
                    i32.store offset=4
                    local.get 1
                    local.get 3
                    i32.store
                    return
                  end
                  local.get 0
                  local.get 2
                  call 15
                end
                block  ;; label = @7
                  block  ;; label = @8
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 1
                        i32.load offset=4
                        local.tee 2
                        i32.const 2
                        i32.and
                        br_if 0 (;@10;)
                        local.get 1
                        i32.const 0
                        i32.load offset=8624
                        i32.eq
                        br_if 2 (;@8;)
                        local.get 1
                        i32.const 0
                        i32.load offset=8620
                        i32.eq
                        br_if 3 (;@7;)
                        local.get 1
                        local.get 2
                        i32.const -8
                        i32.and
                        local.tee 2
                        call 15
                        local.get 0
                        local.get 2
                        local.get 3
                        i32.add
                        local.tee 3
                        i32.const 1
                        i32.or
                        i32.store offset=4
                        local.get 0
                        local.get 3
                        i32.add
                        local.get 3
                        i32.store
                        local.get 0
                        i32.const 0
                        i32.load offset=8620
                        i32.ne
                        br_if 1 (;@9;)
                        i32.const 0
                        local.get 3
                        i32.store offset=8612
                        return
                      end
                      local.get 1
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
                    end
                    local.get 3
                    i32.const 256
                    i32.lt_u
                    br_if 3 (;@5;)
                    i32.const 31
                    local.set 2
                    block  ;; label = @9
                      local.get 3
                      i32.const 16777215
                      i32.gt_u
                      br_if 0 (;@9;)
                      local.get 3
                      i32.const 6
                      local.get 3
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
                    i32.const 8196
                    i32.add
                    local.set 1
                    i32.const 0
                    i32.load offset=8608
                    i32.const 1
                    local.get 2
                    i32.shl
                    local.tee 4
                    i32.and
                    br_if 4 (;@4;)
                    local.get 1
                    local.get 0
                    i32.store
                    local.get 0
                    local.get 1
                    i32.store offset=24
                    local.get 0
                    local.get 0
                    i32.store offset=12
                    local.get 0
                    local.get 0
                    i32.store offset=8
                    i32.const 0
                    i32.const 0
                    i32.load offset=8608
                    local.get 4
                    i32.or
                    i32.store offset=8608
                    br 5 (;@3;)
                  end
                  i32.const 0
                  local.get 0
                  i32.store offset=8624
                  i32.const 0
                  i32.const 0
                  i32.load offset=8616
                  local.get 3
                  i32.add
                  local.tee 3
                  i32.store offset=8616
                  local.get 0
                  local.get 3
                  i32.const 1
                  i32.or
                  i32.store offset=4
                  block  ;; label = @8
                    local.get 0
                    i32.const 0
                    i32.load offset=8620
                    i32.ne
                    br_if 0 (;@8;)
                    i32.const 0
                    i32.const 0
                    i32.store offset=8612
                    i32.const 0
                    i32.const 0
                    i32.store offset=8620
                  end
                  local.get 3
                  i32.const 0
                  i32.load offset=8636
                  local.tee 4
                  i32.le_u
                  br_if 6 (;@1;)
                  i32.const 0
                  i32.load offset=8624
                  local.tee 0
                  i32.eqz
                  br_if 6 (;@1;)
                  i32.const 0
                  local.set 1
                  i32.const 0
                  i32.load offset=8616
                  local.tee 5
                  i32.const 41
                  i32.lt_u
                  br_if 5 (;@2;)
                  i32.const 8324
                  local.set 3
                  loop  ;; label = @8
                    block  ;; label = @9
                      local.get 3
                      i32.load
                      local.tee 2
                      local.get 0
                      i32.gt_u
                      br_if 0 (;@9;)
                      local.get 0
                      local.get 2
                      local.get 3
                      i32.load offset=4
                      i32.add
                      i32.lt_u
                      br_if 7 (;@2;)
                    end
                    local.get 3
                    i32.load offset=8
                    local.set 3
                    br 0 (;@8;)
                  end
                end
                i32.const 0
                local.get 0
                i32.store offset=8620
                i32.const 0
                i32.const 0
                i32.load offset=8612
                local.get 3
                i32.add
                local.tee 3
                i32.store offset=8612
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
                return
              end
              unreachable
            end
            local.get 3
            i32.const 248
            i32.and
            i32.const 8340
            i32.add
            local.set 2
            block  ;; label = @5
              block  ;; label = @6
                i32.const 0
                i32.load offset=8604
                local.tee 1
                i32.const 1
                local.get 3
                i32.const 3
                i32.shr_u
                i32.shl
                local.tee 3
                i32.and
                br_if 0 (;@6;)
                i32.const 0
                local.get 1
                local.get 3
                i32.or
                i32.store offset=8604
                local.get 2
                local.set 3
                br 1 (;@5;)
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
            br 3 (;@1;)
          end
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                local.get 1
                i32.load
                local.tee 4
                i32.load offset=4
                i32.const -8
                i32.and
                local.get 3
                i32.ne
                br_if 0 (;@6;)
                local.get 4
                local.set 2
                br 1 (;@5;)
              end
              local.get 3
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
              local.set 1
              loop  ;; label = @6
                local.get 4
                local.get 1
                i32.const 29
                i32.shr_u
                i32.const 4
                i32.and
                i32.add
                local.tee 5
                i32.load offset=16
                local.tee 2
                i32.eqz
                br_if 2 (;@4;)
                local.get 1
                i32.const 1
                i32.shl
                local.set 1
                local.get 2
                local.set 4
                local.get 2
                i32.load offset=4
                i32.const -8
                i32.and
                local.get 3
                i32.ne
                br_if 0 (;@6;)
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
            br 1 (;@3;)
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
        end
        i32.const 0
        local.set 3
        i32.const 0
        i32.const 0
        i32.load offset=8644
        i32.const -1
        i32.add
        local.tee 0
        i32.store offset=8644
        local.get 0
        br_if 1 (;@1;)
        block  ;; label = @3
          i32.const 0
          i32.load offset=8332
          local.tee 0
          i32.eqz
          br_if 0 (;@3;)
          i32.const 0
          local.set 3
          loop  ;; label = @4
            local.get 3
            i32.const 1
            i32.add
            local.set 3
            local.get 0
            i32.load offset=8
            local.tee 0
            br_if 0 (;@4;)
          end
        end
        i32.const 0
        local.get 3
        i32.const 4095
        local.get 3
        i32.const 4095
        i32.gt_u
        select
        i32.store offset=8644
        return
      end
      block  ;; label = @2
        i32.const 0
        i32.load offset=8332
        local.tee 3
        i32.eqz
        br_if 0 (;@2;)
        i32.const 0
        local.set 1
        loop  ;; label = @3
          local.get 1
          i32.const 1
          i32.add
          local.set 1
          local.get 3
          i32.load offset=8
          local.tee 3
          br_if 0 (;@3;)
        end
      end
      i32.const 0
      local.get 1
      i32.const 4095
      local.get 1
      i32.const 4095
      i32.gt_u
      select
      i32.store offset=8644
      local.get 5
      local.get 4
      i32.le_u
      br_if 0 (;@1;)
      i32.const 0
      i32.const -1
      i32.store offset=8636
      return
    end)
  (func (;14;) (type 2) (param i32 i32)
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
    i32.const 8196
    i32.add
    local.set 3
    block  ;; label = @1
      i32.const 0
      i32.load offset=8608
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
      i32.load offset=8608
      local.get 4
      i32.or
      i32.store offset=8608
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
  (func (;15;) (type 2) (param i32 i32)
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
                i32.const 8196
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
          i32.load offset=8604
          i32.const -2
          local.get 1
          i32.const 3
          i32.shr_u
          i32.rotl
          i32.and
          i32.store offset=8604
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
    i32.load offset=8608
    i32.const -2
    local.get 0
    i32.load offset=28
    i32.rotl
    i32.and
    i32.store offset=8608)
  (table (;0;) 1 1 funcref)
  (memory (;0;) 1)
  (global (;0;) (mut i32) (i32.const 8192))
  (global (;1;) i32 (i32.const 8649))
  (global (;2;) i32 (i32.const 8656))
  (export "memory" (memory 0))
  (export "mark_used" (func 9))
  (export "user_entrypoint" (func 11))
  (export "__data_end" (global 1))
  (export "__heap_base" (global 2))
  (data (;0;) (i32.const 8192) "\02"))
