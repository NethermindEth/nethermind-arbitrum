(module
  (type (;0;) (func (param i32 i32) (result i32)))
  (type (;1;) (func (param i32 i32 i32) (result i32)))
  (type (;2;) (func (param i32 i32)))
  (type (;3;) (func (result i32)))
  (type (;4;) (func (param i32)))
  (type (;5;) (func (param i32 i32 i32)))
  (type (;6;) (func))
  (type (;7;) (func (param i32) (result i32)))
  (type (;8;) (func (param i32 i32 i32 i32 i32) (result i32)))
  (type (;9;) (func (param i32 i32 i32 i32 i32 i32 i32) (result i32)))
  (type (;10;) (func (param i32 i32 i32 i32 i32)))
  (type (;11;) (func (param i32 i32 i32 i32)))
  (import "vm_hooks" "msg_reentrant" (func (;0;) (type 3)))
  (import "vm_hooks" "read_args" (func (;1;) (type 4)))
  (import "vm_hooks" "emit_log" (func (;2;) (type 5)))
  (import "vm_hooks" "storage_flush_cache" (func (;3;) (type 4)))
  (import "vm_hooks" "write_result" (func (;4;) (type 2)))
  (import "vm_hooks" "pay_for_memory_grow" (func (;5;) (type 4)))
  (func (;6;) (type 0) (param i32 i32) (result i32)
    local.get 0
    i32.load
    local.get 0
    i32.load offset=4
    local.get 1
    call 7)
  (func (;7;) (type 1) (param i32 i32 i32) (result i32)
    (local i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32)
    global.get 0
    i32.const 16
    i32.sub
    local.tee 3
    global.set 0
    i32.const 1
    local.set 4
    block  ;; label = @1
      local.get 2
      i32.load
      local.tee 5
      i32.const 34
      local.get 2
      i32.load offset=4
      local.tee 6
      i32.load offset=16
      local.tee 7
      call_indirect (type 0)
      br_if 0 (;@1;)
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            local.get 1
            br_if 0 (;@4;)
            i32.const 0
            local.set 1
            i32.const 0
            local.set 2
            br 1 (;@3;)
          end
          i32.const 0
          local.set 8
          i32.const 0
          local.set 9
          local.get 0
          local.set 10
          local.get 1
          local.set 11
          block  ;; label = @4
            loop  ;; label = @5
              local.get 10
              local.get 11
              i32.add
              local.set 12
              i32.const 0
              local.set 2
              block  ;; label = @6
                loop  ;; label = @7
                  local.get 10
                  local.get 2
                  i32.add
                  local.tee 13
                  i32.load8_u
                  local.tee 14
                  i32.const -127
                  i32.add
                  i32.const 255
                  i32.and
                  i32.const 161
                  i32.lt_u
                  br_if 1 (;@6;)
                  local.get 14
                  i32.const 34
                  i32.eq
                  br_if 1 (;@6;)
                  local.get 14
                  i32.const 92
                  i32.eq
                  br_if 1 (;@6;)
                  local.get 11
                  local.get 2
                  i32.const 1
                  i32.add
                  local.tee 2
                  i32.ne
                  br_if 0 (;@7;)
                end
                local.get 9
                local.get 11
                i32.add
                local.set 9
                br 2 (;@4;)
              end
              local.get 2
              local.get 9
              i32.add
              local.set 9
              block  ;; label = @6
                block  ;; label = @7
                  block  ;; label = @8
                    block  ;; label = @9
                      local.get 13
                      i32.load8_s
                      local.tee 2
                      i32.const -1
                      i32.le_s
                      br_if 0 (;@9;)
                      local.get 13
                      i32.const 1
                      i32.add
                      local.set 10
                      local.get 2
                      i32.const 255
                      i32.and
                      local.set 2
                      br 1 (;@8;)
                    end
                    local.get 13
                    i32.load8_u offset=1
                    i32.const 63
                    i32.and
                    local.set 14
                    local.get 2
                    i32.const 31
                    i32.and
                    local.set 11
                    block  ;; label = @9
                      local.get 2
                      i32.const -33
                      i32.gt_u
                      br_if 0 (;@9;)
                      local.get 11
                      i32.const 6
                      i32.shl
                      local.get 14
                      i32.or
                      local.set 2
                      local.get 13
                      i32.const 2
                      i32.add
                      local.set 10
                      br 1 (;@8;)
                    end
                    local.get 14
                    i32.const 6
                    i32.shl
                    local.get 13
                    i32.load8_u offset=2
                    i32.const 63
                    i32.and
                    i32.or
                    local.set 14
                    block  ;; label = @9
                      local.get 2
                      i32.const -16
                      i32.ge_u
                      br_if 0 (;@9;)
                      local.get 14
                      local.get 11
                      i32.const 12
                      i32.shl
                      i32.or
                      local.set 2
                      local.get 13
                      i32.const 3
                      i32.add
                      local.set 10
                      br 1 (;@8;)
                    end
                    local.get 13
                    i32.const 4
                    i32.add
                    local.set 10
                    local.get 14
                    i32.const 6
                    i32.shl
                    local.get 13
                    i32.load8_u offset=3
                    i32.const 63
                    i32.and
                    i32.or
                    local.get 11
                    i32.const 18
                    i32.shl
                    i32.const 1835008
                    i32.and
                    i32.or
                    local.tee 2
                    i32.const 1114112
                    i32.eq
                    br_if 1 (;@7;)
                  end
                  local.get 3
                  i32.const 4
                  i32.add
                  local.get 2
                  i32.const 65537
                  call 37
                  block  ;; label = @8
                    local.get 3
                    i32.load8_u offset=4
                    i32.const 128
                    i32.eq
                    br_if 0 (;@8;)
                    local.get 3
                    i32.load8_u offset=15
                    local.get 3
                    i32.load8_u offset=14
                    i32.sub
                    i32.const 255
                    i32.and
                    i32.const 1
                    i32.eq
                    br_if 0 (;@8;)
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 9
                        local.get 8
                        i32.lt_u
                        br_if 0 (;@10;)
                        block  ;; label = @11
                          local.get 8
                          i32.eqz
                          br_if 0 (;@11;)
                          block  ;; label = @12
                            local.get 8
                            local.get 1
                            i32.lt_u
                            br_if 0 (;@12;)
                            local.get 8
                            local.get 1
                            i32.ne
                            br_if 2 (;@10;)
                            br 1 (;@11;)
                          end
                          local.get 0
                          local.get 8
                          i32.add
                          i32.load8_s
                          i32.const -65
                          i32.le_s
                          br_if 1 (;@10;)
                        end
                        block  ;; label = @11
                          local.get 9
                          i32.eqz
                          br_if 0 (;@11;)
                          block  ;; label = @12
                            local.get 9
                            local.get 1
                            i32.lt_u
                            br_if 0 (;@12;)
                            local.get 9
                            local.get 1
                            i32.eq
                            br_if 1 (;@11;)
                            br 2 (;@10;)
                          end
                          local.get 0
                          local.get 9
                          i32.add
                          i32.load8_s
                          i32.const -64
                          i32.lt_s
                          br_if 1 (;@10;)
                        end
                        local.get 5
                        local.get 0
                        local.get 8
                        i32.add
                        local.get 9
                        local.get 8
                        i32.sub
                        local.get 6
                        i32.load offset=12
                        local.tee 14
                        call_indirect (type 1)
                        i32.eqz
                        br_if 1 (;@9;)
                        br 4 (;@6;)
                      end
                      local.get 0
                      local.get 1
                      local.get 8
                      local.get 9
                      i32.const 9540
                      call 43
                      unreachable
                    end
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 3
                        i32.load8_u offset=4
                        i32.const 128
                        i32.ne
                        br_if 0 (;@10;)
                        local.get 5
                        local.get 3
                        i32.load offset=8
                        local.get 7
                        call_indirect (type 0)
                        br_if 4 (;@6;)
                        br 1 (;@9;)
                      end
                      local.get 5
                      local.get 3
                      i32.const 4
                      i32.add
                      local.get 3
                      i32.load8_u offset=14
                      local.tee 13
                      i32.add
                      local.get 3
                      i32.load8_u offset=15
                      local.get 13
                      i32.sub
                      local.get 14
                      call_indirect (type 1)
                      br_if 3 (;@6;)
                    end
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 2
                        i32.const 128
                        i32.ge_u
                        br_if 0 (;@10;)
                        i32.const 1
                        local.set 14
                        br 1 (;@9;)
                      end
                      block  ;; label = @10
                        local.get 2
                        i32.const 2048
                        i32.ge_u
                        br_if 0 (;@10;)
                        i32.const 2
                        local.set 14
                        br 1 (;@9;)
                      end
                      i32.const 3
                      i32.const 4
                      local.get 2
                      i32.const 65536
                      i32.lt_u
                      select
                      local.set 14
                    end
                    local.get 14
                    local.get 9
                    i32.add
                    local.set 8
                  end
                  block  ;; label = @8
                    block  ;; label = @9
                      local.get 2
                      i32.const 128
                      i32.ge_u
                      br_if 0 (;@9;)
                      i32.const 1
                      local.set 2
                      br 1 (;@8;)
                    end
                    block  ;; label = @9
                      local.get 2
                      i32.const 2048
                      i32.ge_u
                      br_if 0 (;@9;)
                      i32.const 2
                      local.set 2
                      br 1 (;@8;)
                    end
                    i32.const 3
                    i32.const 4
                    local.get 2
                    i32.const 65536
                    i32.lt_u
                    select
                    local.set 2
                  end
                  local.get 2
                  local.get 9
                  i32.add
                  local.set 9
                end
                local.get 12
                local.get 10
                i32.sub
                local.tee 11
                br_if 1 (;@5;)
                br 2 (;@4;)
              end
            end
            i32.const 1
            local.set 4
            br 3 (;@1;)
          end
          local.get 8
          local.get 9
          i32.gt_u
          br_if 1 (;@2;)
          i32.const 0
          local.set 2
          block  ;; label = @4
            local.get 8
            i32.eqz
            br_if 0 (;@4;)
            block  ;; label = @5
              local.get 8
              local.get 1
              i32.lt_u
              br_if 0 (;@5;)
              local.get 1
              local.set 2
              local.get 8
              local.get 1
              i32.ne
              br_if 3 (;@2;)
              br 1 (;@4;)
            end
            local.get 8
            local.set 2
            local.get 0
            local.get 8
            i32.add
            i32.load8_s
            i32.const -65
            i32.le_s
            br_if 2 (;@2;)
          end
          block  ;; label = @4
            local.get 9
            br_if 0 (;@4;)
            i32.const 0
            local.set 1
            br 1 (;@3;)
          end
          block  ;; label = @4
            local.get 9
            local.get 1
            i32.lt_u
            br_if 0 (;@4;)
            local.get 9
            local.get 1
            i32.eq
            br_if 1 (;@3;)
            local.get 2
            local.set 8
            br 2 (;@2;)
          end
          block  ;; label = @4
            local.get 0
            local.get 9
            i32.add
            i32.load8_s
            i32.const -65
            i32.gt_s
            br_if 0 (;@4;)
            local.get 2
            local.set 8
            br 2 (;@2;)
          end
          local.get 9
          local.set 1
        end
        local.get 5
        local.get 0
        local.get 2
        i32.add
        local.get 1
        local.get 2
        i32.sub
        local.get 6
        i32.load offset=12
        call_indirect (type 1)
        br_if 1 (;@1;)
        local.get 5
        i32.const 34
        local.get 7
        call_indirect (type 0)
        local.set 4
        br 1 (;@1;)
      end
      local.get 0
      local.get 1
      local.get 8
      local.get 9
      i32.const 9556
      call 43
      unreachable
    end
    local.get 3
    i32.const 16
    i32.add
    global.set 0
    local.get 4)
  (func (;8;) (type 6)
    call 9
    call 10
    unreachable)
  (func (;9;) (type 6)
    i32.const 0
    call 5)
  (func (;10;) (type 6)
    call 20
    unreachable)
  (func (;11;) (type 7) (param i32) (result i32)
    (local i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32)
    global.get 0
    i32.const 48
    i32.sub
    local.tee 1
    global.set 0
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          i32.const 0
          i32.load8_u offset=12032
          local.tee 2
          i32.const 2
          i32.ne
          br_if 0 (;@3;)
          i32.const 0
          call 0
          local.tee 2
          i32.store8 offset=12032
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
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              block  ;; label = @6
                block  ;; label = @7
                  local.get 0
                  i32.const -1
                  i32.le_s
                  br_if 0 (;@7;)
                  block  ;; label = @8
                    block  ;; label = @9
                      local.get 0
                      i32.eqz
                      br_if 0 (;@9;)
                      i32.const 0
                      i32.load8_u offset=12501
                      drop
                      i32.const 1
                      local.set 2
                      local.get 0
                      i32.const 1
                      call 12
                      local.tee 4
                      i32.eqz
                      br_if 3 (;@6;)
                      local.get 4
                      call 1
                      local.get 4
                      i32.load8_u
                      local.set 5
                      local.get 1
                      i32.const 0
                      i32.store offset=12
                      local.get 1
                      i64.const 4294967296
                      i64.store offset=4 align=4
                      local.get 0
                      i32.const -1
                      i32.add
                      local.set 6
                      local.get 4
                      i32.const 1
                      i32.add
                      local.set 7
                      local.get 5
                      br_if 1 (;@8;)
                      i32.const 0
                      local.set 5
                      br 6 (;@3;)
                    end
                    i32.const 1
                    call 1
                    call 13
                    unreachable
                  end
                  i32.const 0
                  local.set 3
                  i32.const 1
                  local.set 2
                  i32.const 0
                  local.set 8
                  loop  ;; label = @8
                    local.get 6
                    i32.const 31
                    i32.le_u
                    br_if 3 (;@5;)
                    local.get 1
                    i32.const 16
                    i32.add
                    i32.const 24
                    i32.add
                    local.tee 9
                    local.get 7
                    local.get 3
                    i32.add
                    local.tee 10
                    i32.const 24
                    i32.add
                    i64.load align=1
                    i64.store
                    local.get 1
                    i32.const 16
                    i32.add
                    i32.const 16
                    i32.add
                    local.tee 11
                    local.get 10
                    i32.const 16
                    i32.add
                    i64.load align=1
                    i64.store
                    local.get 1
                    i32.const 16
                    i32.add
                    i32.const 8
                    i32.add
                    local.tee 12
                    local.get 10
                    i32.const 8
                    i32.add
                    i64.load align=1
                    i64.store
                    local.get 1
                    local.get 10
                    i64.load align=1
                    i64.store offset=16
                    block  ;; label = @9
                      local.get 8
                      local.get 1
                      i32.load offset=4
                      i32.ne
                      br_if 0 (;@9;)
                      local.get 1
                      i32.const 4
                      i32.add
                      call 14
                      local.get 1
                      i32.load offset=8
                      local.set 2
                    end
                    local.get 2
                    local.get 3
                    i32.add
                    local.tee 10
                    local.get 1
                    i64.load offset=16
                    i64.store align=1
                    local.get 10
                    i32.const 24
                    i32.add
                    local.get 9
                    i64.load
                    i64.store align=1
                    local.get 10
                    i32.const 16
                    i32.add
                    local.get 11
                    i64.load
                    i64.store align=1
                    local.get 10
                    i32.const 8
                    i32.add
                    local.get 12
                    i64.load
                    i64.store align=1
                    local.get 1
                    local.get 8
                    i32.const 1
                    i32.add
                    local.tee 8
                    i32.store offset=12
                    local.get 6
                    i32.const -32
                    i32.add
                    local.set 6
                    local.get 3
                    i32.const 32
                    i32.add
                    local.set 3
                    local.get 5
                    local.get 8
                    i32.eq
                    br_if 4 (;@4;)
                    br 0 (;@8;)
                  end
                end
                i32.const 0
                local.get 0
                i32.const 12000
                call 15
                unreachable
              end
              i32.const 1
              local.get 0
              i32.const 12000
              call 15
              unreachable
            end
            i32.const 32
            local.get 6
            i32.const 8296
            call 16
            unreachable
          end
          local.get 5
          i32.const 4
          i32.gt_u
          br_if 1 (;@2;)
          local.get 7
          local.get 3
          i32.add
          local.set 7
        end
        i32.const 0
        local.set 11
        local.get 1
        i32.const 0
        i32.store offset=24
        local.get 1
        i64.const 4294967296
        i64.store offset=16 align=4
        local.get 2
        local.get 5
        i32.const 5
        i32.shl
        i32.add
        local.set 9
        i32.const 1
        local.set 13
        i32.const 0
        local.set 8
        block  ;; label = @3
          loop  ;; label = @4
            local.get 2
            local.set 3
            block  ;; label = @5
              block  ;; label = @6
                local.get 8
                i32.eqz
                br_if 0 (;@6;)
                local.get 8
                local.get 10
                i32.ne
                br_if 1 (;@5;)
              end
              local.get 3
              local.get 9
              i32.eq
              br_if 2 (;@3;)
              local.get 3
              i32.const 32
              i32.add
              local.tee 10
              local.set 2
              local.get 3
              local.set 8
              br 1 (;@4;)
            end
            local.get 8
            i32.const 1
            i32.add
            local.set 12
            local.get 8
            i32.load8_u
            local.set 2
            block  ;; label = @5
              local.get 11
              local.get 1
              i32.load offset=16
              i32.ne
              br_if 0 (;@5;)
              local.get 1
              i32.const 16
              i32.add
              local.get 11
              local.get 10
              local.get 12
              i32.sub
              i32.const 1
              i32.add
              local.tee 8
              i32.const -1
              local.get 8
              select
              call 17
              local.get 1
              i32.load offset=20
              local.set 13
            end
            local.get 13
            local.get 11
            i32.add
            local.get 2
            i32.store8
            local.get 1
            local.get 11
            i32.const 1
            i32.add
            local.tee 11
            i32.store offset=24
            local.get 3
            local.set 2
            local.get 12
            local.set 8
            br 0 (;@4;)
          end
        end
        block  ;; label = @3
          local.get 6
          local.get 1
          i32.load offset=16
          local.tee 2
          local.get 11
          i32.sub
          i32.le_u
          br_if 0 (;@3;)
          local.get 1
          i32.const 16
          i32.add
          local.get 11
          local.get 6
          call 17
          local.get 1
          i32.load offset=16
          local.set 2
          local.get 1
          i32.load offset=24
          local.set 11
        end
        local.get 1
        i32.load offset=20
        local.set 3
        block  ;; label = @3
          local.get 6
          i32.eqz
          br_if 0 (;@3;)
          local.get 3
          local.get 11
          i32.add
          local.get 7
          local.get 6
          memory.copy
        end
        local.get 3
        local.get 11
        local.get 6
        i32.add
        local.get 5
        call 2
        block  ;; label = @3
          local.get 2
          i32.eqz
          br_if 0 (;@3;)
          local.get 3
          local.get 2
          call 18
        end
        block  ;; label = @3
          local.get 1
          i32.load offset=4
          local.tee 3
          i32.eqz
          br_if 0 (;@3;)
          local.get 1
          i32.load offset=8
          local.get 3
          i32.const 5
          i32.shl
          call 18
        end
        local.get 4
        local.get 0
        call 18
        i32.const 0
        local.set 3
        i32.const 0
        call 3
        i32.const 1
        i32.const 0
        call 4
        br 1 (;@1;)
      end
      local.get 1
      i32.const 15
      i32.store offset=20
      local.get 1
      i32.const 12016
      i32.store offset=16
      local.get 1
      i32.const 16
      i32.add
      call 19
      unreachable
    end
    local.get 1
    i32.const 48
    i32.add
    global.set 0
    local.get 3)
  (func (;12;) (type 0) (param i32 i32) (result i32)
    local.get 0
    call 53)
  (func (;13;) (type 6)
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
    i32.const 9272
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
    i32.const 8204
    call 25
    unreachable)
  (func (;14;) (type 4) (param i32)
    (local i32 i32 i32 i32 i32 i32 i32)
    global.get 0
    i32.const 32
    i32.sub
    local.tee 1
    global.set 0
    i32.const 0
    local.set 2
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          local.get 0
          i32.load
          local.tee 3
          i32.const 67108863
          i32.le_u
          br_if 0 (;@3;)
          br 1 (;@2;)
        end
        i32.const 0
        local.set 4
        local.get 3
        i32.const 1
        i32.shl
        local.tee 5
        i32.const 4
        local.get 5
        i32.const 4
        i32.gt_u
        select
        local.tee 6
        i32.const 5
        i32.shl
        local.tee 5
        i32.const 0
        i32.lt_s
        br_if 0 (;@2;)
        block  ;; label = @3
          local.get 3
          i32.eqz
          br_if 0 (;@3;)
          local.get 1
          local.get 3
          i32.const 5
          i32.shl
          i32.store offset=28
          local.get 1
          local.get 0
          i32.load offset=4
          i32.store offset=20
          i32.const 1
          local.set 4
        end
        local.get 1
        local.get 4
        i32.store offset=24
        local.get 1
        i32.const 8
        i32.add
        local.get 5
        local.get 1
        i32.const 20
        i32.add
        call 26
        local.get 1
        i32.load offset=8
        i32.const 1
        i32.ne
        br_if 1 (;@1;)
        local.get 1
        i32.load offset=16
        local.set 7
        local.get 1
        i32.load offset=12
        local.set 2
      end
      local.get 2
      local.get 7
      i32.const 8312
      call 15
      unreachable
    end
    local.get 1
    i32.load offset=12
    local.set 3
    local.get 0
    local.get 6
    i32.store
    local.get 0
    local.get 3
    i32.store offset=4
    local.get 1
    i32.const 32
    i32.add
    global.set 0)
  (func (;15;) (type 5) (param i32 i32 i32)
    block  ;; label = @1
      local.get 0
      i32.eqz
      br_if 0 (;@1;)
      local.get 0
      local.get 1
      call 23
      unreachable
    end
    local.get 2
    call 24
    unreachable)
  (func (;16;) (type 5) (param i32 i32 i32)
    local.get 0
    local.get 1
    local.get 2
    call 33
    unreachable)
  (func (;17;) (type 5) (param i32 i32 i32)
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
        call 58
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
      i32.const 11900
      call 15
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
  (func (;18;) (type 2) (param i32 i32)
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
        call 57
        return
      end
      i32.const 11657
      i32.const 46
      i32.const 11704
      call 34
      unreachable
    end
    i32.const 11720
    i32.const 46
    i32.const 11768
    call 34
    unreachable)
  (func (;19;) (type 4) (param i32)
    (local i32)
    global.get 0
    i32.const 64
    i32.sub
    local.tee 1
    global.set 0
    local.get 1
    i32.const 43
    i32.store offset=12
    local.get 1
    i32.const 8236
    i32.store offset=8
    local.get 1
    i32.const 8220
    i32.store offset=20
    local.get 1
    local.get 0
    i32.store offset=16
    local.get 1
    i32.const 2
    i32.store offset=28
    local.get 1
    i32.const 9292
    i32.store offset=24
    local.get 1
    i64.const 2
    i64.store offset=36 align=4
    local.get 1
    i32.const 2
    i64.extend_i32_u
    i64.const 32
    i64.shl
    local.get 1
    i32.const 16
    i32.add
    i64.extend_i32_u
    i64.or
    i64.store offset=56
    local.get 1
    i32.const 3
    i64.extend_i32_u
    i64.const 32
    i64.shl
    local.get 1
    i32.const 8
    i32.add
    i64.extend_i32_u
    i64.or
    i64.store offset=48
    local.get 1
    local.get 1
    i32.const 48
    i32.add
    i32.store offset=32
    local.get 1
    i32.const 24
    i32.add
    i32.const 8280
    call 25
    unreachable)
  (func (;20;) (type 6)
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
    i32.const 9144
    i32.store
    local.get 0
    i64.const 1
    i64.store offset=12 align=4
    local.get 0
    i32.const 3
    i64.extend_i32_u
    i64.const 32
    i64.shl
    i32.const 9212
    i64.extend_i32_u
    i64.or
    i64.store offset=24
    local.get 0
    local.get 0
    i32.const 24
    i32.add
    i32.store offset=8
    local.get 0
    i32.const 8328
    call 25
    unreachable)
  (func (;21;) (type 2) (param i32 i32)
    local.get 0
    local.get 1
    call 22
    unreachable)
  (func (;22;) (type 2) (param i32 i32)
    unreachable)
  (func (;23;) (type 2) (param i32 i32)
    local.get 1
    local.get 0
    call 21
    unreachable)
  (func (;24;) (type 4) (param i32)
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
    i32.const 8364
    i32.store offset=8
    local.get 1
    i64.const 4
    i64.store offset=16 align=4
    local.get 1
    i32.const 8
    i32.add
    local.get 0
    call 25
    unreachable)
  (func (;25;) (type 2) (param i32 i32)
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
    call 30
    unreachable)
  (func (;26;) (type 5) (param i32 i32 i32)
    (local i32)
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            local.get 2
            i32.load offset=4
            i32.eqz
            br_if 0 (;@4;)
            block  ;; label = @5
              local.get 2
              i32.load offset=8
              local.tee 3
              br_if 0 (;@5;)
              local.get 1
              i32.eqz
              br_if 3 (;@2;)
              i32.const 0
              i32.load8_u offset=12501
              drop
              local.get 1
              i32.const 1
              call 12
              local.set 2
              br 2 (;@3;)
            end
            local.get 2
            i32.load
            local.get 3
            local.get 1
            call 27
            local.set 2
            br 1 (;@3;)
          end
          local.get 1
          i32.eqz
          br_if 1 (;@2;)
          i32.const 0
          i32.load8_u offset=12501
          drop
          local.get 1
          i32.const 1
          call 12
          local.set 2
        end
        local.get 2
        i32.const 1
        local.get 2
        select
        local.set 3
        local.get 2
        i32.eqz
        local.set 2
        br 1 (;@1;)
      end
      i32.const 0
      local.set 2
      i32.const 1
      local.set 3
    end
    local.get 0
    local.get 1
    i32.store offset=8
    local.get 0
    local.get 3
    i32.store offset=4
    local.get 0
    local.get 2
    i32.store)
  (func (;27;) (type 1) (param i32 i32 i32) (result i32)
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
                      i32.load offset=12472
                      i32.eq
                      br_if 3 (;@6;)
                      local.get 6
                      i32.const 0
                      i32.load offset=12468
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
                      call 55
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
                      call 56
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
                    call 56
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
                i32.load offset=12460
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
                i32.store offset=12468
                i32.const 0
                local.get 2
                i32.store offset=12460
                local.get 0
                return
              end
              i32.const 0
              i32.load offset=12464
              local.get 5
              i32.add
              local.tee 5
              local.get 1
              i32.gt_u
              br_if 4 (;@1;)
            end
            block  ;; label = @5
              local.get 2
              call 53
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
            call 57
            local.get 5
            local.set 0
          end
          local.get 0
          return
        end
        i32.const 11657
        i32.const 46
        i32.const 11704
        call 34
        unreachable
      end
      i32.const 11720
      i32.const 46
      i32.const 11768
      call 34
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
    i32.store offset=12464
    i32.const 0
    local.get 2
    i32.store offset=12472
    local.get 0)
  (func (;28;) (type 0) (param i32 i32) (result i32)
    local.get 0
    i32.load
    local.get 1
    call 29)
  (func (;29;) (type 0) (param i32 i32) (result i32)
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
        i32.const 9311
        i32.add
        i32.load8_u
        i32.store8
        local.get 6
        i32.const -4
        i32.add
        local.get 9
        i32.const 9310
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
        i32.const 9311
        i32.add
        i32.load8_u
        i32.store8
        local.get 6
        i32.const -2
        i32.add
        local.get 7
        i32.const 9310
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
      i32.const 9311
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
      i32.const 9310
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
      i32.const 9311
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
    call 31
    local.set 5
    local.get 2
    i32.const 16
    i32.add
    global.set 0
    local.get 5)
  (func (;30;) (type 4) (param i32)
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
    call 50
    unreachable)
  (func (;31;) (type 8) (param i32 i32 i32 i32 i32) (result i32)
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
            call 32
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
          call 32
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
      call 32
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
  (func (;32;) (type 8) (param i32 i32 i32 i32 i32) (result i32)
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
  (func (;33;) (type 5) (param i32 i32 i32)
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
    i32.const 11412
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
    call 25
    unreachable)
  (func (;34;) (type 5) (param i32 i32 i32)
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
    call 25
    unreachable)
  (func (;35;) (type 0) (param i32 i32) (result i32)
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
  (func (;36;) (type 0) (param i32 i32) (result i32)
    local.get 0
    i32.load
    local.get 1
    local.get 0
    i32.load offset=4
    i32.load offset=12
    call_indirect (type 0))
  (func (;37;) (type 5) (param i32 i32 i32)
    (local i32 i32)
    global.get 0
    i32.const 32
    i32.sub
    local.tee 3
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
                      block  ;; label = @10
                        block  ;; label = @11
                          block  ;; label = @12
                            block  ;; label = @13
                              block  ;; label = @14
                                local.get 1
                                br_table 6 (;@8;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 2 (;@12;) 4 (;@10;) 1 (;@13;) 1 (;@13;) 3 (;@11;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 9 (;@5;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 1 (;@13;) 7 (;@7;) 0 (;@14;)
                              end
                              local.get 1
                              i32.const 92
                              i32.eq
                              br_if 4 (;@9;)
                            end
                            block  ;; label = @13
                              local.get 2
                              i32.const 1
                              i32.and
                              i32.eqz
                              br_if 0 (;@13;)
                              local.get 1
                              i32.const 767
                              i32.gt_u
                              br_if 7 (;@6;)
                            end
                            local.get 1
                            i32.const 32
                            i32.lt_u
                            br_if 9 (;@3;)
                            local.get 1
                            i32.const 127
                            i32.lt_u
                            br_if 10 (;@2;)
                            br 8 (;@4;)
                          end
                          local.get 0
                          i32.const 512
                          i32.store16 offset=10
                          local.get 0
                          i64.const 0
                          i64.store offset=2 align=2
                          local.get 0
                          i32.const 29788
                          i32.store16
                          br 10 (;@1;)
                        end
                        local.get 0
                        i32.const 512
                        i32.store16 offset=10
                        local.get 0
                        i64.const 0
                        i64.store offset=2 align=2
                        local.get 0
                        i32.const 29276
                        i32.store16
                        br 9 (;@1;)
                      end
                      local.get 0
                      i32.const 512
                      i32.store16 offset=10
                      local.get 0
                      i64.const 0
                      i64.store offset=2 align=2
                      local.get 0
                      i32.const 28252
                      i32.store16
                      br 8 (;@1;)
                    end
                    local.get 0
                    i32.const 512
                    i32.store16 offset=10
                    local.get 0
                    i64.const 0
                    i64.store offset=2 align=2
                    local.get 0
                    i32.const 23644
                    i32.store16
                    br 7 (;@1;)
                  end
                  local.get 0
                  i32.const 512
                  i32.store16 offset=10
                  local.get 0
                  i64.const 0
                  i64.store offset=2 align=2
                  local.get 0
                  i32.const 12380
                  i32.store16
                  br 6 (;@1;)
                end
                local.get 2
                i32.const 256
                i32.and
                i32.eqz
                br_if 4 (;@2;)
                local.get 0
                i32.const 512
                i32.store16 offset=10
                local.get 0
                i64.const 0
                i64.store offset=2 align=2
                local.get 0
                i32.const 10076
                i32.store16
                br 5 (;@1;)
              end
              local.get 1
              call 38
              i32.eqz
              br_if 1 (;@4;)
              local.get 3
              i32.const 0
              i32.store8 offset=10
              local.get 3
              i32.const 0
              i32.store16 offset=8
              local.get 3
              local.get 1
              i32.const 20
              i32.shr_u
              i32.const 9125
              i32.add
              i32.load8_u
              i32.store8 offset=11
              local.get 3
              local.get 1
              i32.const 4
              i32.shr_u
              i32.const 15
              i32.and
              i32.const 9125
              i32.add
              i32.load8_u
              i32.store8 offset=15
              local.get 3
              local.get 1
              i32.const 8
              i32.shr_u
              i32.const 15
              i32.and
              i32.const 9125
              i32.add
              i32.load8_u
              i32.store8 offset=14
              local.get 3
              local.get 1
              i32.const 12
              i32.shr_u
              i32.const 15
              i32.and
              i32.const 9125
              i32.add
              i32.load8_u
              i32.store8 offset=13
              local.get 3
              local.get 1
              i32.const 16
              i32.shr_u
              i32.const 15
              i32.and
              i32.const 9125
              i32.add
              i32.load8_u
              i32.store8 offset=12
              local.get 3
              i32.const 8
              i32.add
              local.get 1
              i32.const 1
              i32.or
              i32.clz
              i32.const 2
              i32.shr_u
              local.tee 2
              i32.add
              local.tee 4
              i32.const 123
              i32.store8
              local.get 4
              i32.const -1
              i32.add
              i32.const 117
              i32.store8
              local.get 3
              i32.const 8
              i32.add
              local.get 2
              i32.const -2
              i32.add
              local.tee 2
              i32.add
              i32.const 92
              i32.store8
              local.get 3
              i32.const 8
              i32.add
              i32.const 8
              i32.add
              local.tee 4
              local.get 1
              i32.const 15
              i32.and
              i32.const 9125
              i32.add
              i32.load8_u
              i32.store8
              local.get 0
              i32.const 10
              i32.store8 offset=11
              local.get 0
              local.get 2
              i32.store8 offset=10
              local.get 0
              local.get 3
              i64.load offset=8 align=4
              i64.store align=4
              local.get 3
              i32.const 125
              i32.store8 offset=17
              local.get 0
              i32.const 8
              i32.add
              local.get 4
              i32.load16_u
              i32.store16
              br 4 (;@1;)
            end
            local.get 2
            i32.const 16777215
            i32.and
            i32.const 65536
            i32.lt_u
            br_if 2 (;@2;)
            local.get 0
            i32.const 512
            i32.store16 offset=10
            local.get 0
            i64.const 0
            i64.store offset=2 align=2
            local.get 0
            i32.const 8796
            i32.store16
            br 3 (;@1;)
          end
          block  ;; label = @4
            local.get 1
            i32.const 65536
            i32.lt_u
            br_if 0 (;@4;)
            block  ;; label = @5
              local.get 1
              i32.const 131072
              i32.ge_u
              br_if 0 (;@5;)
              local.get 1
              i32.const 9912
              i32.const 44
              i32.const 10000
              i32.const 208
              i32.const 10208
              i32.const 486
              call 39
              i32.eqz
              br_if 2 (;@3;)
              br 3 (;@2;)
            end
            local.get 1
            i32.const 2097150
            i32.and
            i32.const 178206
            i32.eq
            br_if 1 (;@3;)
            local.get 1
            i32.const 2097120
            i32.and
            i32.const 173792
            i32.eq
            br_if 1 (;@3;)
            local.get 1
            i32.const -177984
            i32.add
            i32.const -7
            i32.gt_u
            br_if 1 (;@3;)
            local.get 1
            i32.const -183984
            i32.add
            i32.const -15
            i32.gt_u
            br_if 1 (;@3;)
            local.get 1
            i32.const -191472
            i32.add
            i32.const -16
            i32.gt_u
            br_if 1 (;@3;)
            local.get 1
            i32.const -194560
            i32.add
            i32.const -2467
            i32.gt_u
            br_if 1 (;@3;)
            local.get 1
            i32.const -196608
            i32.add
            i32.const -1507
            i32.gt_u
            br_if 1 (;@3;)
            local.get 1
            i32.const -201552
            i32.add
            i32.const -6
            i32.gt_u
            br_if 1 (;@3;)
            local.get 1
            i32.const -917760
            i32.add
            i32.const -712017
            i32.gt_u
            br_if 1 (;@3;)
            local.get 1
            i32.const 918000
            i32.ge_u
            br_if 1 (;@3;)
            br 2 (;@2;)
          end
          local.get 1
          i32.const 10694
          i32.const 40
          i32.const 10774
          i32.const 290
          i32.const 11064
          i32.const 297
          call 39
          br_if 1 (;@2;)
        end
        local.get 3
        i32.const 0
        i32.store8 offset=22
        local.get 3
        i32.const 0
        i32.store16 offset=20
        local.get 3
        local.get 1
        i32.const 20
        i32.shr_u
        i32.const 9125
        i32.add
        i32.load8_u
        i32.store8 offset=23
        local.get 3
        local.get 1
        i32.const 4
        i32.shr_u
        i32.const 15
        i32.and
        i32.const 9125
        i32.add
        i32.load8_u
        i32.store8 offset=27
        local.get 3
        local.get 1
        i32.const 8
        i32.shr_u
        i32.const 15
        i32.and
        i32.const 9125
        i32.add
        i32.load8_u
        i32.store8 offset=26
        local.get 3
        local.get 1
        i32.const 12
        i32.shr_u
        i32.const 15
        i32.and
        i32.const 9125
        i32.add
        i32.load8_u
        i32.store8 offset=25
        local.get 3
        local.get 1
        i32.const 16
        i32.shr_u
        i32.const 15
        i32.and
        i32.const 9125
        i32.add
        i32.load8_u
        i32.store8 offset=24
        local.get 3
        i32.const 20
        i32.add
        local.get 1
        i32.const 1
        i32.or
        i32.clz
        i32.const 2
        i32.shr_u
        local.tee 2
        i32.add
        local.tee 4
        i32.const 123
        i32.store8
        local.get 4
        i32.const -1
        i32.add
        i32.const 117
        i32.store8
        local.get 3
        i32.const 20
        i32.add
        local.get 2
        i32.const -2
        i32.add
        local.tee 2
        i32.add
        i32.const 92
        i32.store8
        local.get 3
        i32.const 20
        i32.add
        i32.const 8
        i32.add
        local.tee 4
        local.get 1
        i32.const 15
        i32.and
        i32.const 9125
        i32.add
        i32.load8_u
        i32.store8
        local.get 0
        i32.const 10
        i32.store8 offset=11
        local.get 0
        local.get 2
        i32.store8 offset=10
        local.get 0
        local.get 3
        i64.load offset=20 align=4
        i64.store align=4
        local.get 3
        i32.const 125
        i32.store8 offset=29
        local.get 0
        i32.const 8
        i32.add
        local.get 4
        i32.load16_u
        i32.store16
        br 1 (;@1;)
      end
      local.get 0
      local.get 1
      i32.store offset=4
      local.get 0
      i32.const 128
      i32.store8
    end
    local.get 3
    i32.const 32
    i32.add
    global.set 0)
  (func (;38;) (type 7) (param i32) (result i32)
    (local i32 i32 i32 i32 i32)
    i32.const 0
    local.set 1
    i32.const 0
    i32.const 17
    local.get 0
    i32.const 71727
    i32.lt_u
    select
    local.tee 2
    local.get 2
    i32.const 8
    i32.or
    local.tee 2
    local.get 2
    i32.const 2
    i32.shl
    i32.const 11480
    i32.add
    i32.load
    i32.const 11
    i32.shl
    local.get 0
    i32.const 11
    i32.shl
    local.tee 2
    i32.gt_u
    select
    local.tee 3
    local.get 3
    i32.const 4
    i32.or
    local.tee 3
    local.get 3
    i32.const 2
    i32.shl
    i32.const 11480
    i32.add
    i32.load
    i32.const 11
    i32.shl
    local.get 2
    i32.gt_u
    select
    local.tee 3
    local.get 3
    i32.const 2
    i32.or
    local.tee 3
    local.get 3
    i32.const 2
    i32.shl
    i32.const 11480
    i32.add
    i32.load
    i32.const 11
    i32.shl
    local.get 2
    i32.gt_u
    select
    local.tee 3
    local.get 3
    i32.const 1
    i32.add
    local.tee 3
    local.get 3
    i32.const 2
    i32.shl
    i32.const 11480
    i32.add
    i32.load
    i32.const 11
    i32.shl
    local.get 2
    i32.gt_u
    select
    local.tee 3
    local.get 3
    i32.const 1
    i32.add
    local.tee 3
    local.get 3
    i32.const 2
    i32.shl
    i32.const 11480
    i32.add
    i32.load
    i32.const 11
    i32.shl
    local.get 2
    i32.gt_u
    select
    local.tee 3
    i32.const 2
    i32.shl
    i32.const 11480
    i32.add
    i32.load
    i32.const 11
    i32.shl
    local.tee 4
    local.get 2
    i32.eq
    local.get 4
    local.get 2
    i32.lt_u
    i32.add
    local.get 3
    i32.add
    local.tee 3
    i32.const 2
    i32.shl
    i32.const 11480
    i32.add
    local.tee 5
    i32.load
    i32.const 21
    i32.shr_u
    local.set 2
    i32.const 751
    local.set 4
    block  ;; label = @1
      block  ;; label = @2
        local.get 3
        i32.const 32
        i32.gt_u
        br_if 0 (;@2;)
        local.get 5
        i32.load offset=4
        i32.const 21
        i32.shr_u
        local.set 4
        local.get 3
        i32.eqz
        br_if 1 (;@1;)
      end
      local.get 5
      i32.const -4
      i32.add
      i32.load
      i32.const 2097151
      i32.and
      local.set 1
    end
    block  ;; label = @1
      local.get 4
      local.get 2
      i32.const 1
      i32.add
      i32.eq
      br_if 0 (;@1;)
      local.get 0
      local.get 1
      i32.sub
      local.set 3
      local.get 4
      i32.const -1
      i32.add
      local.set 4
      i32.const 0
      local.set 0
      loop  ;; label = @2
        local.get 0
        local.get 2
        i32.const 8372
        i32.add
        i32.load8_u
        i32.add
        local.tee 0
        local.get 3
        i32.gt_u
        br_if 1 (;@1;)
        local.get 4
        local.get 2
        i32.const 1
        i32.add
        local.tee 2
        i32.ne
        br_if 0 (;@2;)
      end
    end
    local.get 2
    i32.const 1
    i32.and)
  (func (;39;) (type 9) (param i32 i32 i32 i32 i32 i32 i32) (result i32)
    (local i32 i32 i32 i32 i32 i32)
    local.get 1
    local.get 2
    i32.const 1
    i32.shl
    i32.add
    local.set 7
    local.get 0
    i32.const 65280
    i32.and
    i32.const 8
    i32.shr_u
    local.set 8
    i32.const 0
    local.set 9
    local.get 0
    i32.const 255
    i32.and
    local.set 10
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            loop  ;; label = @5
              local.get 1
              i32.const 2
              i32.add
              local.set 11
              local.get 9
              local.get 1
              i32.load8_u offset=1
              local.tee 2
              i32.add
              local.set 12
              block  ;; label = @6
                local.get 1
                i32.load8_u
                local.tee 1
                local.get 8
                i32.eq
                br_if 0 (;@6;)
                local.get 1
                local.get 8
                i32.gt_u
                br_if 4 (;@2;)
                local.get 12
                local.set 9
                local.get 11
                local.set 1
                local.get 11
                local.get 7
                i32.ne
                br_if 1 (;@5;)
                br 4 (;@2;)
              end
              local.get 12
              local.get 9
              i32.lt_u
              br_if 1 (;@4;)
              local.get 12
              local.get 4
              i32.gt_u
              br_if 2 (;@3;)
              local.get 3
              local.get 9
              i32.add
              local.set 1
              loop  ;; label = @6
                block  ;; label = @7
                  local.get 2
                  br_if 0 (;@7;)
                  local.get 12
                  local.set 9
                  local.get 11
                  local.set 1
                  local.get 11
                  local.get 7
                  i32.ne
                  br_if 2 (;@5;)
                  br 5 (;@2;)
                end
                local.get 2
                i32.const -1
                i32.add
                local.set 2
                local.get 1
                i32.load8_u
                local.set 9
                local.get 1
                i32.const 1
                i32.add
                local.set 1
                local.get 9
                local.get 10
                i32.ne
                br_if 0 (;@6;)
              end
            end
            i32.const 0
            local.set 2
            br 3 (;@1;)
          end
          local.get 9
          local.get 12
          i32.const 9896
          call 40
          unreachable
        end
        local.get 12
        local.get 4
        i32.const 9896
        call 16
        unreachable
      end
      local.get 0
      i32.const 65535
      i32.and
      local.set 9
      local.get 5
      local.get 6
      i32.add
      local.set 12
      i32.const 1
      local.set 2
      loop  ;; label = @2
        local.get 5
        i32.const 1
        i32.add
        local.set 10
        block  ;; label = @3
          block  ;; label = @4
            local.get 5
            i32.load8_s
            local.tee 1
            i32.const 0
            i32.lt_s
            br_if 0 (;@4;)
            local.get 10
            local.set 5
            br 1 (;@3;)
          end
          block  ;; label = @4
            local.get 10
            local.get 12
            i32.eq
            br_if 0 (;@4;)
            local.get 1
            i32.const 127
            i32.and
            i32.const 8
            i32.shl
            local.get 5
            i32.load8_u offset=1
            i32.or
            local.set 1
            local.get 5
            i32.const 2
            i32.add
            local.set 5
            br 1 (;@3;)
          end
          i32.const 9880
          call 41
          unreachable
        end
        local.get 9
        local.get 1
        i32.sub
        local.tee 9
        i32.const 0
        i32.lt_s
        br_if 1 (;@1;)
        local.get 2
        i32.const 1
        i32.xor
        local.set 2
        local.get 5
        local.get 12
        i32.ne
        br_if 0 (;@2;)
      end
    end
    local.get 2
    i32.const 1
    i32.and)
  (func (;40;) (type 5) (param i32 i32 i32)
    local.get 0
    local.get 1
    local.get 2
    call 42
    unreachable)
  (func (;41;) (type 4) (param i32)
    i32.const 9152
    i32.const 43
    local.get 0
    call 34
    unreachable)
  (func (;42;) (type 5) (param i32 i32 i32)
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
    i32.const 11464
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
    call 25
    unreachable)
  (func (;43;) (type 10) (param i32 i32 i32 i32 i32)
    local.get 0
    local.get 1
    local.get 2
    local.get 3
    local.get 4
    call 44
    unreachable)
  (func (;44;) (type 10) (param i32 i32 i32 i32 i32)
    (local i32 i32 i32 i32 i64)
    global.get 0
    i32.const 112
    i32.sub
    local.tee 5
    global.set 0
    local.get 5
    local.get 3
    i32.store offset=12
    local.get 5
    local.get 2
    i32.store offset=8
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          local.get 1
          i32.const 257
          i32.lt_u
          br_if 0 (;@3;)
          block  ;; label = @4
            local.get 0
            i32.load8_s offset=256
            i32.const -65
            i32.le_s
            br_if 0 (;@4;)
            i32.const 256
            local.set 6
            br 2 (;@2;)
          end
          block  ;; label = @4
            local.get 0
            i32.load8_s offset=255
            i32.const -65
            i32.le_s
            br_if 0 (;@4;)
            i32.const 255
            local.set 6
            br 2 (;@2;)
          end
          local.get 0
          i32.const 254
          i32.const 253
          local.get 0
          i32.load8_s offset=254
          i32.const -65
          i32.gt_s
          select
          local.tee 6
          i32.add
          i32.load8_s
          i32.const -65
          i32.gt_s
          br_if 1 (;@2;)
          local.get 0
          local.get 1
          i32.const 0
          local.get 6
          local.get 4
          call 43
          unreachable
        end
        i32.const 0
        local.set 7
        i32.const 1
        local.set 8
        local.get 1
        local.set 6
        br 1 (;@1;)
      end
      i32.const 5
      local.set 7
      i32.const 9599
      local.set 8
    end
    local.get 5
    local.get 6
    i32.store offset=20
    local.get 5
    local.get 0
    i32.store offset=16
    local.get 5
    local.get 7
    i32.store offset=28
    local.get 5
    local.get 8
    i32.store offset=24
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            block  ;; label = @5
              local.get 2
              local.get 1
              i32.gt_u
              local.tee 6
              br_if 0 (;@5;)
              local.get 3
              local.get 1
              i32.gt_u
              br_if 0 (;@5;)
              local.get 2
              local.get 3
              i32.gt_u
              br_if 1 (;@4;)
              block  ;; label = @6
                local.get 2
                i32.eqz
                br_if 0 (;@6;)
                local.get 2
                local.get 1
                i32.ge_u
                br_if 0 (;@6;)
                local.get 3
                local.get 2
                local.get 0
                local.get 2
                i32.add
                i32.load8_s
                i32.const -65
                i32.gt_s
                select
                local.set 3
              end
              local.get 5
              local.get 3
              i32.store offset=32
              local.get 3
              local.get 1
              i32.ge_u
              br_if 4 (;@1;)
              local.get 3
              i32.const 1
              i32.add
              local.tee 6
              i32.const 0
              local.get 3
              i32.const -3
              i32.add
              local.tee 2
              local.get 2
              local.get 3
              i32.gt_u
              select
              local.tee 2
              i32.lt_u
              br_if 2 (;@3;)
              local.get 6
              local.get 2
              i32.sub
              local.set 7
              block  ;; label = @6
                block  ;; label = @7
                  local.get 0
                  local.get 3
                  i32.add
                  i32.load8_s
                  i32.const -65
                  i32.le_s
                  br_if 0 (;@7;)
                  local.get 7
                  i32.const -1
                  i32.add
                  local.set 3
                  br 1 (;@6;)
                end
                block  ;; label = @7
                  local.get 0
                  local.get 6
                  i32.add
                  local.tee 3
                  i32.const -2
                  i32.add
                  i32.load8_s
                  i32.const -65
                  i32.le_s
                  br_if 0 (;@7;)
                  local.get 7
                  i32.const -2
                  i32.add
                  local.set 3
                  br 1 (;@6;)
                end
                block  ;; label = @7
                  local.get 3
                  i32.const -3
                  i32.add
                  i32.load8_s
                  i32.const -65
                  i32.le_s
                  br_if 0 (;@7;)
                  local.get 7
                  i32.const -3
                  i32.add
                  local.set 3
                  br 1 (;@6;)
                end
                local.get 7
                i32.const -4
                i32.const -5
                local.get 3
                i32.const -4
                i32.add
                i32.load8_s
                i32.const -65
                i32.gt_s
                select
                i32.add
                local.set 3
              end
              block  ;; label = @6
                block  ;; label = @7
                  local.get 3
                  local.get 2
                  i32.add
                  local.tee 3
                  br_if 0 (;@7;)
                  local.get 0
                  local.set 2
                  i32.const 0
                  local.set 3
                  br 1 (;@6;)
                end
                block  ;; label = @7
                  local.get 3
                  local.get 1
                  i32.lt_u
                  br_if 0 (;@7;)
                  local.get 3
                  local.get 1
                  i32.ne
                  br_if 5 (;@2;)
                  br 6 (;@1;)
                end
                local.get 0
                local.get 3
                i32.add
                local.tee 2
                i32.load8_s
                i32.const -65
                i32.le_s
                br_if 4 (;@2;)
                local.get 3
                local.get 1
                i32.eq
                br_if 5 (;@1;)
              end
              block  ;; label = @6
                block  ;; label = @7
                  block  ;; label = @8
                    block  ;; label = @9
                      local.get 2
                      i32.load8_s
                      local.tee 1
                      i32.const -1
                      i32.gt_s
                      br_if 0 (;@9;)
                      local.get 2
                      i32.load8_u offset=1
                      i32.const 63
                      i32.and
                      local.set 0
                      local.get 1
                      i32.const 31
                      i32.and
                      local.set 6
                      local.get 1
                      i32.const -33
                      i32.gt_u
                      br_if 1 (;@8;)
                      local.get 6
                      i32.const 6
                      i32.shl
                      local.get 0
                      i32.or
                      local.set 1
                      br 2 (;@7;)
                    end
                    local.get 5
                    local.get 1
                    i32.const 255
                    i32.and
                    i32.store offset=36
                    i32.const 1
                    local.set 1
                    br 2 (;@6;)
                  end
                  local.get 0
                  i32.const 6
                  i32.shl
                  local.get 2
                  i32.load8_u offset=2
                  i32.const 63
                  i32.and
                  i32.or
                  local.set 0
                  block  ;; label = @8
                    local.get 1
                    i32.const -16
                    i32.ge_u
                    br_if 0 (;@8;)
                    local.get 0
                    local.get 6
                    i32.const 12
                    i32.shl
                    i32.or
                    local.set 1
                    br 1 (;@7;)
                  end
                  local.get 0
                  i32.const 6
                  i32.shl
                  local.get 2
                  i32.load8_u offset=3
                  i32.const 63
                  i32.and
                  i32.or
                  local.get 6
                  i32.const 18
                  i32.shl
                  i32.const 1835008
                  i32.and
                  i32.or
                  local.tee 1
                  i32.const 1114112
                  i32.eq
                  br_if 6 (;@1;)
                end
                local.get 5
                local.get 1
                i32.store offset=36
                block  ;; label = @7
                  local.get 1
                  i32.const 128
                  i32.ge_u
                  br_if 0 (;@7;)
                  i32.const 1
                  local.set 1
                  br 1 (;@6;)
                end
                block  ;; label = @7
                  local.get 1
                  i32.const 2048
                  i32.ge_u
                  br_if 0 (;@7;)
                  i32.const 2
                  local.set 1
                  br 1 (;@6;)
                end
                i32.const 3
                i32.const 4
                local.get 1
                i32.const 65536
                i32.lt_u
                select
                local.set 1
              end
              local.get 5
              local.get 3
              i32.store offset=40
              local.get 5
              local.get 1
              local.get 3
              i32.add
              i32.store offset=44
              local.get 5
              i32.const 5
              i32.store offset=52
              local.get 5
              i32.const 9736
              i32.store offset=48
              local.get 5
              i64.const 5
              i64.store offset=60 align=4
              local.get 5
              i32.const 3
              i64.extend_i32_u
              i64.const 32
              i64.shl
              local.tee 9
              local.get 5
              i32.const 24
              i32.add
              i64.extend_i32_u
              i64.or
              i64.store offset=104
              local.get 5
              local.get 9
              local.get 5
              i32.const 16
              i32.add
              i64.extend_i32_u
              i64.or
              i64.store offset=96
              local.get 5
              i32.const 4
              i64.extend_i32_u
              i64.const 32
              i64.shl
              local.get 5
              i32.const 40
              i32.add
              i64.extend_i32_u
              i64.or
              i64.store offset=88
              local.get 5
              i32.const 5
              i64.extend_i32_u
              i64.const 32
              i64.shl
              local.get 5
              i32.const 36
              i32.add
              i64.extend_i32_u
              i64.or
              i64.store offset=80
              local.get 5
              i32.const 1
              i64.extend_i32_u
              i64.const 32
              i64.shl
              local.get 5
              i32.const 32
              i32.add
              i64.extend_i32_u
              i64.or
              i64.store offset=72
              local.get 5
              local.get 5
              i32.const 72
              i32.add
              i32.store offset=56
              local.get 5
              i32.const 48
              i32.add
              local.get 4
              call 25
              unreachable
            end
            local.get 5
            local.get 2
            local.get 3
            local.get 6
            select
            i32.store offset=40
            local.get 5
            i32.const 3
            i32.store offset=52
            local.get 5
            i32.const 9800
            i32.store offset=48
            local.get 5
            i64.const 3
            i64.store offset=60 align=4
            local.get 5
            i32.const 3
            i64.extend_i32_u
            i64.const 32
            i64.shl
            local.tee 9
            local.get 5
            i32.const 24
            i32.add
            i64.extend_i32_u
            i64.or
            i64.store offset=88
            local.get 5
            local.get 9
            local.get 5
            i32.const 16
            i32.add
            i64.extend_i32_u
            i64.or
            i64.store offset=80
            local.get 5
            i32.const 1
            i64.extend_i32_u
            i64.const 32
            i64.shl
            local.get 5
            i32.const 40
            i32.add
            i64.extend_i32_u
            i64.or
            i64.store offset=72
            local.get 5
            local.get 5
            i32.const 72
            i32.add
            i32.store offset=56
            local.get 5
            i32.const 48
            i32.add
            local.get 4
            call 25
            unreachable
          end
          local.get 5
          i32.const 4
          i32.store offset=52
          local.get 5
          i32.const 9640
          i32.store offset=48
          local.get 5
          i64.const 4
          i64.store offset=60 align=4
          local.get 5
          i32.const 3
          i64.extend_i32_u
          i64.const 32
          i64.shl
          local.tee 9
          local.get 5
          i32.const 24
          i32.add
          i64.extend_i32_u
          i64.or
          i64.store offset=96
          local.get 5
          local.get 9
          local.get 5
          i32.const 16
          i32.add
          i64.extend_i32_u
          i64.or
          i64.store offset=88
          local.get 5
          i32.const 1
          i64.extend_i32_u
          i64.const 32
          i64.shl
          local.tee 9
          local.get 5
          i32.const 12
          i32.add
          i64.extend_i32_u
          i64.or
          i64.store offset=80
          local.get 5
          local.get 9
          local.get 5
          i32.const 8
          i32.add
          i64.extend_i32_u
          i64.or
          i64.store offset=72
          local.get 5
          local.get 5
          i32.const 72
          i32.add
          i32.store offset=56
          local.get 5
          i32.const 48
          i32.add
          local.get 4
          call 25
          unreachable
        end
        local.get 2
        local.get 6
        i32.const 9824
        call 40
        unreachable
      end
      local.get 0
      local.get 1
      local.get 3
      local.get 1
      local.get 4
      call 43
      unreachable
    end
    local.get 4
    call 41
    unreachable)
  (func (;45;) (type 0) (param i32 i32) (result i32)
    (local i32 i32 i32 i32)
    global.get 0
    i32.const 128
    i32.sub
    local.tee 2
    global.set 0
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          block  ;; label = @4
            local.get 1
            i32.load offset=8
            local.tee 3
            i32.const 33554432
            i32.and
            br_if 0 (;@4;)
            local.get 3
            i32.const 67108864
            i32.and
            br_if 1 (;@3;)
            local.get 0
            i32.load
            local.get 1
            call 29
            i32.eqz
            br_if 2 (;@2;)
            i32.const 1
            local.set 3
            br 3 (;@1;)
          end
          local.get 0
          i32.load
          local.set 3
          i32.const 129
          local.set 4
          loop  ;; label = @4
            local.get 2
            local.get 4
            i32.add
            i32.const -2
            i32.add
            local.get 3
            i32.const 15
            i32.and
            local.tee 5
            i32.const 48
            i32.or
            local.get 5
            i32.const 87
            i32.add
            local.get 5
            i32.const 10
            i32.lt_u
            select
            i32.store8
            local.get 4
            i32.const -1
            i32.add
            local.set 4
            local.get 3
            i32.const 16
            i32.lt_u
            local.set 5
            local.get 3
            i32.const 4
            i32.shr_u
            local.set 3
            local.get 5
            i32.eqz
            br_if 0 (;@4;)
          end
          local.get 1
          i32.const 9308
          i32.const 2
          local.get 2
          local.get 4
          i32.add
          i32.const -1
          i32.add
          i32.const 129
          local.get 4
          i32.sub
          call 31
          i32.eqz
          br_if 1 (;@2;)
          i32.const 1
          local.set 3
          br 2 (;@1;)
        end
        local.get 0
        i32.load
        local.set 3
        i32.const 129
        local.set 4
        loop  ;; label = @3
          local.get 2
          local.get 4
          i32.add
          i32.const -2
          i32.add
          local.get 3
          i32.const 15
          i32.and
          local.tee 5
          i32.const 48
          i32.or
          local.get 5
          i32.const 55
          i32.add
          local.get 5
          i32.const 10
          i32.lt_u
          select
          i32.store8
          local.get 4
          i32.const -1
          i32.add
          local.set 4
          local.get 3
          i32.const 15
          i32.gt_u
          local.set 5
          local.get 3
          i32.const 4
          i32.shr_u
          local.set 3
          local.get 5
          br_if 0 (;@3;)
        end
        local.get 1
        i32.const 9308
        i32.const 2
        local.get 2
        local.get 4
        i32.add
        i32.const -1
        i32.add
        i32.const 129
        local.get 4
        i32.sub
        call 31
        i32.eqz
        br_if 0 (;@2;)
        i32.const 1
        local.set 3
        br 1 (;@1;)
      end
      block  ;; label = @2
        local.get 1
        i32.load
        i32.const 9123
        i32.const 2
        local.get 1
        i32.load offset=4
        i32.load offset=12
        call_indirect (type 1)
        i32.eqz
        br_if 0 (;@2;)
        i32.const 1
        local.set 3
        br 1 (;@1;)
      end
      block  ;; label = @2
        block  ;; label = @3
          local.get 1
          i32.load offset=8
          local.tee 3
          i32.const 33554432
          i32.and
          br_if 0 (;@3;)
          local.get 3
          i32.const 67108864
          i32.and
          br_if 1 (;@2;)
          local.get 0
          i32.load offset=4
          local.get 1
          call 29
          local.set 3
          br 2 (;@1;)
        end
        local.get 0
        i32.load offset=4
        local.set 3
        i32.const 129
        local.set 4
        loop  ;; label = @3
          local.get 2
          local.get 4
          i32.add
          i32.const -2
          i32.add
          local.get 3
          i32.const 15
          i32.and
          local.tee 5
          i32.const 48
          i32.or
          local.get 5
          i32.const 87
          i32.add
          local.get 5
          i32.const 10
          i32.lt_u
          select
          i32.store8
          local.get 4
          i32.const -1
          i32.add
          local.set 4
          local.get 3
          i32.const 15
          i32.gt_u
          local.set 5
          local.get 3
          i32.const 4
          i32.shr_u
          local.set 3
          local.get 5
          br_if 0 (;@3;)
        end
        local.get 1
        i32.const 9308
        i32.const 2
        local.get 2
        local.get 4
        i32.add
        i32.const -1
        i32.add
        i32.const 129
        local.get 4
        i32.sub
        call 31
        local.set 3
        br 1 (;@1;)
      end
      local.get 0
      i32.load offset=4
      local.set 3
      i32.const 129
      local.set 4
      loop  ;; label = @2
        local.get 2
        local.get 4
        i32.add
        i32.const -2
        i32.add
        local.get 3
        i32.const 15
        i32.and
        local.tee 5
        i32.const 48
        i32.or
        local.get 5
        i32.const 55
        i32.add
        local.get 5
        i32.const 10
        i32.lt_u
        select
        i32.store8
        local.get 4
        i32.const -1
        i32.add
        local.set 4
        local.get 3
        i32.const 15
        i32.gt_u
        local.set 5
        local.get 3
        i32.const 4
        i32.shr_u
        local.set 3
        local.get 5
        br_if 0 (;@2;)
      end
      local.get 1
      i32.const 9308
      i32.const 2
      local.get 2
      local.get 4
      i32.add
      i32.const -1
      i32.add
      i32.const 129
      local.get 4
      i32.sub
      call 31
      local.set 3
    end
    local.get 2
    i32.const 128
    i32.add
    global.set 0
    local.get 3)
  (func (;46;) (type 0) (param i32 i32) (result i32)
    (local i32 i32 i32 i32)
    global.get 0
    i32.const 16
    i32.sub
    local.tee 2
    global.set 0
    i32.const 1
    local.set 3
    block  ;; label = @1
      local.get 1
      i32.load
      local.tee 4
      i32.const 39
      local.get 1
      i32.load offset=4
      local.tee 5
      i32.load offset=16
      local.tee 1
      call_indirect (type 0)
      br_if 0 (;@1;)
      local.get 2
      i32.const 4
      i32.add
      local.get 0
      i32.load
      i32.const 257
      call 37
      block  ;; label = @2
        block  ;; label = @3
          local.get 2
          i32.load8_u offset=4
          i32.const 128
          i32.ne
          br_if 0 (;@3;)
          local.get 4
          local.get 2
          i32.load offset=8
          local.get 1
          call_indirect (type 0)
          i32.eqz
          br_if 1 (;@2;)
          i32.const 1
          local.set 3
          br 2 (;@1;)
        end
        local.get 4
        local.get 2
        i32.const 4
        i32.add
        local.get 2
        i32.load8_u offset=14
        local.tee 3
        i32.add
        local.get 2
        i32.load8_u offset=15
        local.get 3
        i32.sub
        local.get 5
        i32.load offset=12
        call_indirect (type 1)
        i32.eqz
        br_if 0 (;@2;)
        i32.const 1
        local.set 3
        br 1 (;@1;)
      end
      local.get 4
      i32.const 39
      local.get 1
      call_indirect (type 0)
      local.set 3
    end
    local.get 2
    i32.const 16
    i32.add
    global.set 0
    local.get 3)
  (func (;47;) (type 2) (param i32 i32)
    local.get 0
    i32.const 0
    i32.store)
  (func (;48;) (type 11) (param i32 i32 i32 i32)
    (local i32 i32)
    global.get 0
    i32.const 16
    i32.sub
    local.tee 4
    global.set 0
    i32.const 0
    i32.const 0
    i32.load offset=12040
    local.tee 5
    i32.const 1
    i32.add
    i32.store offset=12040
    block  ;; label = @1
      local.get 5
      i32.const 0
      i32.lt_s
      br_if 0 (;@1;)
      block  ;; label = @2
        block  ;; label = @3
          i32.const 0
          i32.load8_u offset=12500
          br_if 0 (;@3;)
          i32.const 0
          i32.const 0
          i32.load offset=12496
          i32.const 1
          i32.add
          i32.store offset=12496
          i32.const 0
          i32.load offset=12036
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
      i32.store8 offset=12500
      local.get 2
      i32.eqz
      br_if 0 (;@1;)
      call 49
      unreachable
    end
    unreachable)
  (func (;49;) (type 6)
    unreachable)
  (func (;50;) (type 4) (param i32)
    local.get 0
    call 51
    unreachable)
  (func (;51;) (type 4) (param i32)
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
      i32.const 6
      local.get 0
      i32.load offset=8
      local.tee 0
      i32.load8_u offset=8
      local.get 0
      i32.load8_u offset=9
      call 48
      unreachable
    end
    local.get 1
    local.get 3
    i32.store offset=4
    local.get 1
    local.get 2
    i32.store
    local.get 1
    i32.const 7
    local.get 0
    i32.load offset=8
    local.tee 0
    i32.load8_u offset=8
    local.get 0
    i32.load8_u offset=9
    call 48
    unreachable)
  (func (;52;) (type 2) (param i32 i32)
    local.get 0
    local.get 1
    i64.load align=4
    i64.store)
  (func (;53;) (type 7) (param i32) (result i32)
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
                    i32.load offset=12452
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
                    i32.load offset=12460
                    i32.le_u
                    br_if 7 (;@1;)
                    local.get 0
                    br_if 2 (;@6;)
                    i32.const 0
                    i32.load offset=12456
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
                  i32.load offset=12456
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
                    i32.const 12044
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
                    i32.const 12188
                    i32.add
                    local.tee 2
                    local.get 0
                    i32.const 12196
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
                  i32.store offset=12452
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
                  i32.const 12188
                  i32.add
                  local.tee 6
                  local.get 3
                  i32.const 12196
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
                i32.store offset=12452
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
                i32.load offset=12460
                local.tee 1
                i32.eqz
                br_if 0 (;@6;)
                local.get 1
                i32.const -8
                i32.and
                i32.const 12188
                i32.add
                local.set 6
                i32.const 0
                i32.load offset=12468
                local.set 3
                block  ;; label = @7
                  block  ;; label = @8
                    i32.const 0
                    i32.load offset=12452
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
                    i32.store offset=12452
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
              i32.store offset=12468
              i32.const 0
              local.get 2
              i32.store offset=12460
              local.get 0
              i32.const 8
              i32.add
              return
            end
            local.get 0
            i32.ctz
            i32.const 2
            i32.shl
            i32.const 12044
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
                        i32.const 12044
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
              i32.load offset=12456
              i32.const -2
              local.get 1
              i32.load offset=28
              i32.rotl
              i32.and
              i32.store offset=12456
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
                  i32.load offset=12460
                  local.tee 7
                  i32.eqz
                  br_if 1 (;@6;)
                  local.get 7
                  i32.const -8
                  i32.and
                  i32.const 12188
                  i32.add
                  local.set 6
                  i32.const 0
                  i32.load offset=12468
                  local.set 0
                  block  ;; label = @8
                    block  ;; label = @9
                      i32.const 0
                      i32.load offset=12452
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
                      i32.store offset=12452
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
              i32.store offset=12468
              i32.const 0
              local.get 3
              i32.store offset=12460
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
            i32.const 12044
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
        i32.load offset=12460
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
              i32.const 12044
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
        i32.load offset=12456
        i32.const -2
        local.get 6
        i32.load offset=28
        i32.rotl
        i32.and
        i32.store offset=12456
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
            i32.const 12044
            i32.add
            local.set 1
            block  ;; label = @5
              i32.const 0
              i32.load offset=12456
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
              i32.load offset=12456
              local.get 7
              i32.or
              i32.store offset=12456
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
          i32.const 12188
          i32.add
          local.set 0
          block  ;; label = @4
            block  ;; label = @5
              i32.const 0
              i32.load offset=12452
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
              i32.store offset=12452
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
                  i32.load offset=12460
                  local.tee 0
                  local.get 2
                  i32.ge_u
                  br_if 0 (;@7;)
                  block  ;; label = @8
                    i32.const 0
                    i32.load offset=12464
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
                    i32.load offset=12476
                    i32.const 0
                    local.get 6
                    i32.const -65536
                    i32.and
                    local.get 7
                    select
                    local.tee 8
                    i32.add
                    local.tee 0
                    i32.store offset=12476
                    i32.const 0
                    local.get 0
                    i32.const 0
                    i32.load offset=12480
                    local.tee 3
                    local.get 0
                    local.get 3
                    i32.gt_u
                    select
                    i32.store offset=12480
                    block  ;; label = @9
                      block  ;; label = @10
                        block  ;; label = @11
                          i32.const 0
                          i32.load offset=12472
                          local.tee 3
                          i32.eqz
                          br_if 0 (;@11;)
                          i32.const 12172
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
                            i32.load offset=12488
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
                          i32.store offset=12488
                        end
                        i32.const 0
                        i32.const 4095
                        i32.store offset=12492
                        i32.const 0
                        local.get 8
                        i32.store offset=12176
                        i32.const 0
                        local.get 1
                        i32.store offset=12172
                        i32.const 0
                        i32.const 12188
                        i32.store offset=12200
                        i32.const 0
                        i32.const 12196
                        i32.store offset=12208
                        i32.const 0
                        i32.const 12188
                        i32.store offset=12196
                        i32.const 0
                        i32.const 12204
                        i32.store offset=12216
                        i32.const 0
                        i32.const 12196
                        i32.store offset=12204
                        i32.const 0
                        i32.const 12212
                        i32.store offset=12224
                        i32.const 0
                        i32.const 12204
                        i32.store offset=12212
                        i32.const 0
                        i32.const 12220
                        i32.store offset=12232
                        i32.const 0
                        i32.const 12212
                        i32.store offset=12220
                        i32.const 0
                        i32.const 12228
                        i32.store offset=12240
                        i32.const 0
                        i32.const 12220
                        i32.store offset=12228
                        i32.const 0
                        i32.const 12236
                        i32.store offset=12248
                        i32.const 0
                        i32.const 12228
                        i32.store offset=12236
                        i32.const 0
                        i32.const 12244
                        i32.store offset=12256
                        i32.const 0
                        i32.const 12236
                        i32.store offset=12244
                        i32.const 0
                        i32.const 0
                        i32.store offset=12184
                        i32.const 0
                        i32.const 12252
                        i32.store offset=12264
                        i32.const 0
                        i32.const 12244
                        i32.store offset=12252
                        i32.const 0
                        i32.const 12252
                        i32.store offset=12260
                        i32.const 0
                        i32.const 12260
                        i32.store offset=12272
                        i32.const 0
                        i32.const 12260
                        i32.store offset=12268
                        i32.const 0
                        i32.const 12268
                        i32.store offset=12280
                        i32.const 0
                        i32.const 12268
                        i32.store offset=12276
                        i32.const 0
                        i32.const 12276
                        i32.store offset=12288
                        i32.const 0
                        i32.const 12276
                        i32.store offset=12284
                        i32.const 0
                        i32.const 12284
                        i32.store offset=12296
                        i32.const 0
                        i32.const 12284
                        i32.store offset=12292
                        i32.const 0
                        i32.const 12292
                        i32.store offset=12304
                        i32.const 0
                        i32.const 12292
                        i32.store offset=12300
                        i32.const 0
                        i32.const 12300
                        i32.store offset=12312
                        i32.const 0
                        i32.const 12300
                        i32.store offset=12308
                        i32.const 0
                        i32.const 12308
                        i32.store offset=12320
                        i32.const 0
                        i32.const 12308
                        i32.store offset=12316
                        i32.const 0
                        i32.const 12316
                        i32.store offset=12328
                        i32.const 0
                        i32.const 12324
                        i32.store offset=12336
                        i32.const 0
                        i32.const 12316
                        i32.store offset=12324
                        i32.const 0
                        i32.const 12332
                        i32.store offset=12344
                        i32.const 0
                        i32.const 12324
                        i32.store offset=12332
                        i32.const 0
                        i32.const 12340
                        i32.store offset=12352
                        i32.const 0
                        i32.const 12332
                        i32.store offset=12340
                        i32.const 0
                        i32.const 12348
                        i32.store offset=12360
                        i32.const 0
                        i32.const 12340
                        i32.store offset=12348
                        i32.const 0
                        i32.const 12356
                        i32.store offset=12368
                        i32.const 0
                        i32.const 12348
                        i32.store offset=12356
                        i32.const 0
                        i32.const 12364
                        i32.store offset=12376
                        i32.const 0
                        i32.const 12356
                        i32.store offset=12364
                        i32.const 0
                        i32.const 12372
                        i32.store offset=12384
                        i32.const 0
                        i32.const 12364
                        i32.store offset=12372
                        i32.const 0
                        i32.const 12380
                        i32.store offset=12392
                        i32.const 0
                        i32.const 12372
                        i32.store offset=12380
                        i32.const 0
                        i32.const 12388
                        i32.store offset=12400
                        i32.const 0
                        i32.const 12380
                        i32.store offset=12388
                        i32.const 0
                        i32.const 12396
                        i32.store offset=12408
                        i32.const 0
                        i32.const 12388
                        i32.store offset=12396
                        i32.const 0
                        i32.const 12404
                        i32.store offset=12416
                        i32.const 0
                        i32.const 12396
                        i32.store offset=12404
                        i32.const 0
                        i32.const 12412
                        i32.store offset=12424
                        i32.const 0
                        i32.const 12404
                        i32.store offset=12412
                        i32.const 0
                        i32.const 12420
                        i32.store offset=12432
                        i32.const 0
                        i32.const 12412
                        i32.store offset=12420
                        i32.const 0
                        i32.const 12428
                        i32.store offset=12440
                        i32.const 0
                        i32.const 12420
                        i32.store offset=12428
                        i32.const 0
                        i32.const 12436
                        i32.store offset=12448
                        i32.const 0
                        i32.const 12428
                        i32.store offset=12436
                        i32.const 0
                        local.get 1
                        i32.store offset=12472
                        i32.const 0
                        i32.const 12436
                        i32.store offset=12444
                        i32.const 0
                        local.get 8
                        i32.const -40
                        i32.add
                        local.tee 0
                        i32.store offset=12464
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
                        i32.store offset=12484
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
                    i32.load offset=12488
                    local.tee 0
                    local.get 1
                    local.get 0
                    local.get 1
                    i32.lt_u
                    select
                    i32.store offset=12488
                    local.get 1
                    local.get 8
                    i32.add
                    local.set 6
                    i32.const 12172
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
                      i32.const 12172
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
                      i32.store offset=12472
                      i32.const 0
                      local.get 8
                      i32.const -40
                      i32.add
                      local.tee 0
                      i32.store offset=12464
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
                      i32.store offset=12484
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
                      i64.load offset=12172 align=4
                      local.set 9
                      local.get 7
                      i32.const 16
                      i32.add
                      i32.const 0
                      i64.load offset=12180 align=4
                      i64.store align=4
                      local.get 7
                      local.get 9
                      i64.store offset=8 align=4
                      i32.const 0
                      local.get 8
                      i32.store offset=12176
                      i32.const 0
                      local.get 1
                      i32.store offset=12172
                      i32.const 0
                      local.get 7
                      i32.const 8
                      i32.add
                      i32.store offset=12180
                      i32.const 0
                      i32.const 0
                      i32.store offset=12184
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
                        call 54
                        br 8 (;@2;)
                      end
                      local.get 0
                      i32.const 248
                      i32.and
                      i32.const 12188
                      i32.add
                      local.set 6
                      block  ;; label = @10
                        block  ;; label = @11
                          i32.const 0
                          i32.load offset=12452
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
                          i32.store offset=12452
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
                    i32.load offset=12472
                    i32.eq
                    br_if 3 (;@5;)
                    local.get 6
                    i32.const 0
                    i32.load offset=12468
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
                      call 55
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
                      call 54
                      br 6 (;@3;)
                    end
                    local.get 3
                    i32.const 248
                    i32.and
                    i32.const 12188
                    i32.add
                    local.set 2
                    block  ;; label = @9
                      block  ;; label = @10
                        i32.const 0
                        i32.load offset=12452
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
                        i32.store offset=12452
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
                  i32.store offset=12464
                  i32.const 0
                  i32.const 0
                  i32.load offset=12472
                  local.tee 0
                  local.get 2
                  i32.add
                  local.tee 6
                  i32.store offset=12472
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
                i32.load offset=12468
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
                    i32.store offset=12468
                    i32.const 0
                    i32.const 0
                    i32.store offset=12460
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
                  i32.store offset=12460
                  i32.const 0
                  local.get 3
                  local.get 2
                  i32.add
                  local.tee 1
                  i32.store offset=12468
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
              i32.load offset=12472
              local.tee 0
              i32.const 15
              i32.add
              i32.const -8
              i32.and
              local.tee 3
              i32.const -8
              i32.add
              local.tee 6
              i32.store offset=12472
              i32.const 0
              local.get 0
              local.get 3
              i32.sub
              i32.const 0
              i32.load offset=12464
              local.get 8
              i32.add
              local.tee 3
              i32.add
              i32.const 8
              i32.add
              local.tee 1
              i32.store offset=12464
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
              i32.store offset=12484
              br 3 (;@2;)
            end
            i32.const 0
            local.get 0
            i32.store offset=12472
            i32.const 0
            i32.const 0
            i32.load offset=12464
            local.get 3
            i32.add
            local.tee 3
            i32.store offset=12464
            local.get 0
            local.get 3
            i32.const 1
            i32.or
            i32.store offset=4
            br 1 (;@3;)
          end
          i32.const 0
          local.get 0
          i32.store offset=12468
          i32.const 0
          i32.const 0
          i32.load offset=12460
          local.get 3
          i32.add
          local.tee 3
          i32.store offset=12460
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
      i32.load offset=12464
      local.tee 3
      local.get 2
      i32.le_u
      br_if 0 (;@1;)
      i32.const 0
      local.get 3
      local.get 2
      i32.sub
      local.tee 3
      i32.store offset=12464
      i32.const 0
      i32.const 0
      i32.load offset=12472
      local.tee 0
      local.get 2
      i32.add
      local.tee 6
      i32.store offset=12472
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
  (func (;54;) (type 2) (param i32 i32)
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
    i32.const 12044
    i32.add
    local.set 3
    block  ;; label = @1
      i32.const 0
      i32.load offset=12456
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
      i32.load offset=12456
      local.get 4
      i32.or
      i32.store offset=12456
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
  (func (;55;) (type 2) (param i32 i32)
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
                i32.const 12044
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
          i32.load offset=12452
          i32.const -2
          local.get 1
          i32.const 3
          i32.shr_u
          i32.rotl
          i32.and
          i32.store offset=12452
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
    i32.load offset=12456
    i32.const -2
    local.get 0
    i32.load offset=28
    i32.rotl
    i32.and
    i32.store offset=12456)
  (func (;56;) (type 2) (param i32 i32)
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
          i32.load offset=12468
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
          i32.store offset=12460
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
        call 55
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
              i32.load offset=12472
              i32.eq
              br_if 2 (;@3;)
              local.get 2
              i32.const 0
              i32.load offset=12468
              i32.eq
              br_if 3 (;@2;)
              local.get 2
              local.get 3
              i32.const -8
              i32.and
              local.tee 3
              call 55
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
              i32.load offset=12468
              i32.ne
              br_if 1 (;@4;)
              i32.const 0
              local.get 1
              i32.store offset=12460
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
            i32.const 12044
            i32.add
            local.set 3
            block  ;; label = @5
              i32.const 0
              i32.load offset=12456
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
              i32.load offset=12456
              local.get 4
              i32.or
              i32.store offset=12456
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
          i32.const 12188
          i32.add
          local.set 2
          block  ;; label = @4
            block  ;; label = @5
              i32.const 0
              i32.load offset=12452
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
              i32.store offset=12452
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
        i32.store offset=12472
        i32.const 0
        i32.const 0
        i32.load offset=12464
        local.get 1
        i32.add
        local.tee 1
        i32.store offset=12464
        local.get 0
        local.get 1
        i32.const 1
        i32.or
        i32.store offset=4
        local.get 0
        i32.const 0
        i32.load offset=12468
        i32.ne
        br_if 1 (;@1;)
        i32.const 0
        i32.const 0
        i32.store offset=12460
        i32.const 0
        i32.const 0
        i32.store offset=12468
        return
      end
      i32.const 0
      local.get 0
      i32.store offset=12468
      i32.const 0
      i32.const 0
      i32.load offset=12460
      local.get 1
      i32.add
      local.tee 1
      i32.store offset=12460
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
  (func (;57;) (type 4) (param i32)
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
          i32.load offset=12468
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
          i32.store offset=12460
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
        call 55
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
                      i32.load offset=12472
                      i32.eq
                      br_if 2 (;@7;)
                      local.get 3
                      i32.const 0
                      i32.load offset=12468
                      i32.eq
                      br_if 3 (;@6;)
                      local.get 3
                      local.get 2
                      i32.const -8
                      i32.and
                      local.tee 2
                      call 55
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
                      i32.load offset=12468
                      i32.ne
                      br_if 1 (;@8;)
                      i32.const 0
                      local.get 0
                      i32.store offset=12460
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
                  i32.const 12044
                  i32.add
                  local.set 2
                  i32.const 0
                  i32.load offset=12456
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
                  i32.load offset=12456
                  local.get 4
                  i32.or
                  i32.store offset=12456
                  br 4 (;@3;)
                end
                i32.const 0
                local.get 1
                i32.store offset=12472
                i32.const 0
                i32.const 0
                i32.load offset=12464
                local.get 0
                i32.add
                local.tee 0
                i32.store offset=12464
                local.get 1
                local.get 0
                i32.const 1
                i32.or
                i32.store offset=4
                block  ;; label = @7
                  local.get 1
                  i32.const 0
                  i32.load offset=12468
                  i32.ne
                  br_if 0 (;@7;)
                  i32.const 0
                  i32.const 0
                  i32.store offset=12460
                  i32.const 0
                  i32.const 0
                  i32.store offset=12468
                end
                local.get 0
                i32.const 0
                i32.load offset=12484
                local.tee 4
                i32.le_u
                br_if 5 (;@1;)
                i32.const 0
                i32.load offset=12472
                local.tee 0
                i32.eqz
                br_if 5 (;@1;)
                i32.const 0
                local.set 2
                i32.const 0
                i32.load offset=12464
                local.tee 5
                i32.const 41
                i32.lt_u
                br_if 4 (;@2;)
                i32.const 12172
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
              i32.store offset=12468
              i32.const 0
              i32.const 0
              i32.load offset=12460
              local.get 0
              i32.add
              local.tee 0
              i32.store offset=12460
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
            i32.const 12188
            i32.add
            local.set 3
            block  ;; label = @5
              block  ;; label = @6
                i32.const 0
                i32.load offset=12452
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
                i32.store offset=12452
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
        i32.load offset=12492
        i32.const -1
        i32.add
        local.tee 0
        i32.store offset=12492
        local.get 0
        br_if 1 (;@1;)
        block  ;; label = @3
          i32.const 0
          i32.load offset=12180
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
        i32.store offset=12492
        return
      end
      block  ;; label = @2
        i32.const 0
        i32.load offset=12180
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
      i32.store offset=12492
      local.get 5
      local.get 4
      i32.le_u
      br_if 0 (;@1;)
      i32.const 0
      i32.const -1
      i32.store offset=12484
    end)
  (func (;58;) (type 5) (param i32 i32 i32)
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
          i32.load8_u offset=12501
          drop
          local.get 1
          i32.const 1
          call 12
          local.set 2
          br 2 (;@1;)
        end
        local.get 2
        i32.load
        local.get 3
        local.get 1
        call 27
        local.set 2
        br 1 (;@1;)
      end
      i32.const 0
      i32.load8_u offset=12501
      drop
      local.get 1
      i32.const 1
      call 12
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
  (table (;0;) 9 9 funcref)
  (memory (;0;) 1)
  (global (;0;) (mut i32) (i32.const 8192))
  (global (;1;) i32 (i32.const 12502))
  (global (;2;) i32 (i32.const 12512))
  (export "memory" (memory 0))
  (export "mark_used" (func 8))
  (export "user_entrypoint" (func 11))
  (export "__data_end" (global 1))
  (export "__heap_base" (global 2))
  (elem (;0;) (i32.const 1) func 28 36 35 45 46 47 52 6)
  (data (;0;) (i32.const 8192) "src/main.rs\00\00 \00\00\0b\00\00\00\0a\00\00\00\1b\00\00\00\00\00\00\00\08\00\00\00\04\00\00\00\08\00\00\00called `Result::unwrap()` on an `Err` value\00\00 \00\00\0b\00\00\00\12\00\00\00\22\00\00\00\00 \00\00\0b\00\00\00\0f\00\00\00*\00\00\00\00 \00\00\0b\00\00\00\0f\00\00\00\10\00\00\00\00 \00\00\0b\00\00\00\08\00\00\00\01\00\00\00capacity overflow\00\00\00\98 \00\00\11\00\00\00\00p\00\07\00-\01\01\01\02\01\02\01\01H\0b0\15\10\01e\07\02\06\02\02\01\04#\01\1e\1b[\0b:\09\09\01\18\04\01\09\01\03\01\05+\03;\09*\18\01 7\01\01\01\04\08\04\01\03\07\0a\02\1d\01:\01\01\01\02\04\08\01\09\01\0a\02\1a\01\02\029\01\04\02\04\02\02\03\03\01\1e\02\03\01\0b\029\01\04\05\01\02\04\01\14\02\16\06\01\01:\01\01\02\01\04\08\01\07\03\0a\02\1e\01;\01\01\01\0c\01\09\01(\01\03\017\01\01\03\05\03\01\04\07\02\0b\02\1d\01:\01\02\02\01\01\03\03\01\04\07\02\0b\02\1c\029\02\01\01\02\04\08\01\09\01\0a\02\1d\01H\01\04\01\02\03\01\01\08\01Q\01\02\07\0c\08b\01\02\09\0b\07I\02\1b\01\01\01\01\017\0e\01\05\01\02\05\0b\01$\09\01f\04\01\06\01\02\02\02\19\02\04\03\10\04\0d\01\02\02\06\01\0f\01\00\03\00\04\1c\03\1d\02\1e\02@\02\01\07\08\01\02\0b\09\01-\03\01\01u\02\22\01v\03\04\02\09\01\06\03\db\02\02\01:\01\01\07\01\01\01\01\02\08\06\0a\02\010\1f1\040\0a\04\03&\09\0c\02 \04\02\068\01\01\02\03\01\01\058\08\02\02\98\03\01\0d\01\07\04\01\06\01\03\02\c6@\00\01\c3!\00\03\8d\01` \00\06i\02\00\04\01\0a \02P\02\00\01\03\01\04\01\19\02\05\01\97\02\1a\12\0d\01&\08\19\0b\01\01,\030\01\02\04\02\02\02\01$\01C\06\02\02\02\02\0c\01\08\01/\013\01\01\03\02\02\05\02\01\01*\02\08\01\ee\01\02\01\04\01\00\01\00\10\10\10\00\02\00\01\e2\01\95\05\00\03\01\02\05\04(\03\04\01\a5\02\00\04A\05\00\02O\04F\0b1\04{\016\0f)\01\02\02\0a\031\04\02\02\07\01=\03$\05\01\08>\01\0c\024\09\01\01\08\04\02\01_\03\02\04\06\01\02\01\9d\01\03\08\15\029\02\01\01\01\01\0c\01\09\01\0e\07\03\05C\01\02\06\01\01\02\01\01\03\04\03\01\01\0e\02U\08\02\03\01\01\17\01Q\01\02\06\01\01\02\01\01\02\01\02\eb\01\02\04\06\02\01\02\1b\02U\08\02\01\01\02j\01\01\01\02\08e\01\01\01\02\04\01\05\00\09\01\02\f5\01\0a\04\04\01\90\04\02\02\04\01 \0a(\06\02\04\08\01\09\06\02\03.\0d\01\02\00\07\01\06\01\01R\16\02\07\01\02\01\02z\06\03\01\01\02\01\07\01\01H\02\03\01\01\01\00\02\0b\024\05\05\03\17\01\00\01\06\0f\00\0c\03\03\00\05;\07\00\01?\04Q\01\0b\02\00\02\00.\02\17\00\05\03\06\08\08\02\07\1e\04\94\03\007\042\08\01\0e\01\16\05\01\0f\00\07\01\11\02\07\01\02\01\05d\01\a0\07\00\01=\04\00\04\fe\02\00\07m\07\00`\80\f0\00..0123456789abcdef\00\00\00\01\00\00\00\00\00\00\00called `Option::unwrap()` on a `None` valueexplicit panic\00\00\00\eb#\00\00\0e\00\00\00index out of bounds: the len is  but the index is \00\00\04$\00\00 \00\00\00$$\00\00\12\00\00\00: \00\00\01\00\00\00\00\00\00\00H$\00\00\02\00\00\000x00010203040506070809101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899library/core/src/fmt/mod.rs\00\00\00&%\00\00\1b\00\00\00\99\0a\00\00&\00\00\00&%\00\00\1b\00\00\00\a2\0a\00\00\1a\00\00\00library/core/src/str/mod.rs[...]begin <= end ( <= ) when slicing ``\00\84%\00\00\0e\00\00\00\92%\00\00\04\00\00\00\96%\00\00\10\00\00\00\a6%\00\00\01\00\00\00byte index  is not a char boundary; it is inside  (bytes ) of `\00\c8%\00\00\0b\00\00\00\d3%\00\00&\00\00\00\f9%\00\00\08\00\00\00\01&\00\00\06\00\00\00\a6%\00\00\01\00\00\00 is out of bounds of `\00\00\c8%\00\00\0b\00\00\000&\00\00\16\00\00\00\a6%\00\00\01\00\00\00d%\00\00\1b\00\00\00\9e\01\00\00,\00\00\00library/core/src/unicode/printable.rs\00\00\00p&\00\00%\00\00\00\1a\00\00\006\00\00\00p&\00\00%\00\00\00\0a\00\00\00+\00\00\00\00\06\01\01\03\01\04\02\05\07\07\02\08\08\09\02\0a\05\0b\02\0e\04\10\01\11\02\12\05\13\1c\14\01\15\02\17\02\19\0d\1c\05\1d\08\1f\01$\01j\04k\02\af\03\b1\02\bc\02\cf\02\d1\02\d4\0c\d5\09\d6\02\d7\02\da\01\e0\05\e1\02\e7\04\e8\02\ee \f0\04\f8\02\fa\04\fb\01\0c';>NO\8f\9e\9e\9f{\8b\93\96\a2\b2\ba\86\b1\06\07\096=>V\f3\d0\d1\04\14\1867VW\7f\aa\ae\af\bd5\e0\12\87\89\8e\9e\04\0d\0e\11\12)14:EFIJNOde\8a\8c\8d\8f\b6\c1\c3\c4\c6\cb\d6\5c\b6\b7\1b\1c\07\08\0a\0b\14\1769:\a8\a9\d8\d9\097\90\91\a8\07\0a;>fi\8f\92\11o_\bf\ee\efZb\f4\fc\ffST\9a\9b./'(U\9d\a0\a1\a3\a4\a7\a8\ad\ba\bc\c4\06\0b\0c\15\1d:?EQ\a6\a7\cc\cd\a0\07\19\1a\22%>?\e7\ec\ef\ff\c5\c6\04 #%&(38:HJLPSUVXZ\5c^`cefksx}\7f\8a\a4\aa\af\b0\c0\d0\ae\afno\dd\de\93^\22{\05\03\04-\03f\03\01/.\80\82\1d\031\0f\1c\04$\09\1e\05+\05D\04\0e*\80\aa\06$\04$\04(\084\0bN\034\0c\817\09\16\0a\08\18;E9\03c\08\090\16\05!\03\1b\05\01@8\04K\05/\04\0a\07\09\07@ '\04\0c\096\03:\05\1a\07\04\0c\07PI73\0d3\07.\08\0a\06&\03\1d\08\02\80\d0R\10\037,\08*\16\1a&\1c\14\17\09N\04$\09D\0d\19\07\0a\06H\08'\09u\0bB>*\06;\05\0a\06Q\06\01\05\10\03\05\0bY\08\02\1db\1eH\08\0a\80\a6^\22E\0b\0a\06\0d\13:\06\0a\06\14\1c,\04\17\80\b9<dS\0cH\09\0aFE\1bH\08S\0dI\07\0a\80\b6\22\0e\0a\06F\0a\1d\03GI7\03\0e\08\0a\069\07\0a\816\19\07;\03\1dU\01\0f2\0d\83\9bfu\0b\80\c4\8aLc\0d\840\10\16\0a\8f\9b\05\82G\9a\b9:\86\c6\829\07*\04\5c\06&\0aF\0a(\05\13\81\b0:\80\c6[eK\049\07\11@\05\0b\02\0e\97\f8\08\84\d6)\0a\a2\e7\813\0f\01\1d\06\0e\04\08\81\8c\89\04k\05\0d\03\09\07\10\8f`\80\fa\06\81\b4LG\09t<\80\f6\0as\08p\15Fz\14\0c\14\0cW\09\19\80\87\81G\03\85B\0f\15\84P\1f\06\06\80\d5+\05>!\01p-\03\1a\04\02\81@\1f\11:\05\01\81\d0*\80\d6+\04\01\81\e0\80\f7)L\04\0a\04\02\83\11DL=\80\c2<\06\01\04U\05\1b4\02\81\0e,\04d\0cV\0a\80\ae8\1d\0d,\04\09\07\02\0e\06\80\9a\83\d8\04\11\03\0d\03w\04_\06\0c\04\01\0f\0c\048\08\0a\06(\08,\04\02>\81T\0c\1d\03\0a\058\07\1c\06\09\07\80\fa\84\06\00\01\03\05\05\06\06\02\07\06\08\07\09\11\0a\1c\0b\19\0c\1a\0d\10\0e\0c\0f\04\10\03\12\12\13\09\16\01\17\04\18\01\19\03\1a\07\1b\01\1c\02\1f\16 \03+\03-\0b.\010\041\022\01\a7\04\a9\02\aa\04\ab\08\fa\02\fb\05\fd\02\fe\03\ff\09\adxy\8b\8d\a20WX\8b\8c\90\1c\dd\0e\0fKL\fb\fc./?\5c]_\e2\84\8d\8e\91\92\a9\b1\ba\bb\c5\c6\c9\ca\de\e4\e5\ff\00\04\11\12)147:;=IJ]\84\8e\92\a9\b1\b4\ba\bb\c6\ca\ce\cf\e4\e5\00\04\0d\0e\11\12)14:;EFIJ^de\84\91\9b\9d\c9\ce\cf\0d\11):;EIW[\5c^_de\8d\91\a9\b4\ba\bb\c5\c9\df\e4\e5\f0\0d\11EIde\80\84\b2\bc\be\bf\d5\d7\f0\f1\83\85\8b\a4\a6\be\bf\c5\c7\cf\da\dbH\98\bd\cd\c6\ce\cfINOWY^_\89\8e\8f\b1\b6\b7\bf\c1\c6\c7\d7\11\16\17[\5c\f6\f7\fe\ff\80mq\de\df\0e\1fno\1c\1d_}~\ae\afM\bb\bc\16\17\1e\1fFGNOXZ\5c^~\7f\b5\c5\d4\d5\dc\f0\f1\f5rs\8ftu\96&./\a7\af\b7\bf\c7\cf\d7\df\9a\00@\97\980\8f\1f\ce\cf\d2\d4\ce\ffNOZ[\07\08\0f\10'/\ee\efno7=?BE\90\91Sgu\c8\c9\d0\d1\d8\d9\e7\fe\ff\00 _\22\82\df\04\82D\08\1b\04\06\11\81\ac\0e\80\ab\05\1f\08\81\1c\03\19\08\01\04/\044\04\07\03\01\07\06\07\11\0aP\0f\12\07U\07\03\04\1c\0a\09\03\08\03\07\03\02\03\03\03\0c\04\05\03\0b\06\01\0e\15\05N\07\1b\07W\07\02\06\17\0cP\04C\03-\03\01\04\11\06\0f\0c:\04\1d%_ m\04j%\80\c8\05\82\b0\03\1a\06\82\fd\03Y\07\16\09\18\09\14\0c\14\0cj\06\0a\06\1a\06Y\07+\05F\0a,\04\0c\04\01\031\0b,\04\1a\06\0b\03\80\ac\06\0a\06/1\80\f4\08<\03\0f\03>\058\08+\05\82\ff\11\18\08/\11-\03!\0f!\0f\80\8c\04\82\9a\16\0b\15\88\94\05/\05;\07\02\0e\18\09\80\be\22t\0c\80\d6\1a\81\10\05\80\e1\09\f2\9e\037\09\81\5c\14\80\b8\08\80\dd\15;\03\0a\068\08F\08\0c\06t\0b\1e\03Z\04Y\09\80\83\18\1c\0a\16\09L\04\80\8a\06\ab\a4\0c\17\041\a1\04\81\da&\07\0c\05\05\80\a6\10\81\f5\07\01 *\06L\04\80\8d\04\80\be\03\1b\03\0f\0d out of range for slice of length range end index \00\83,\00\00\10\00\00\00a,\00\00\22\00\00\00slice index starts at  but ends at \00\a4,\00\00\16\00\00\00\ba,\00\00\0d\00\00\00\00\03\00\00\83\04 \00\91\05`\00]\13\a0\00\12\17 \1f\0c `\1f\ef, +*0\a0+o\a6`,\02\a8\e0,\1e\fb\e0-\00\fe 6\9e\ff`6\fd\01\e16\01\0a!7$\0d\e17\ab\0ea9/\18\e190\1c\e1J\f3\1e\e1N@4\a1R\1ea\e1S\f0jaTOo\e1T\9d\bcaU\00\cfaVe\d1\a1V\00\da!W\00\e0\a1X\ae\e2!Z\ec\e4\e1[\d0\e8a\5c \00\ee\5c\f0\01\7f]/rust/deps/dlmalloc-0.2.8/src/dlmalloc.rsassertion failed: psize >= size + min_overhead\00`-\00\00)\00\00\00\ac\04\00\00\09\00\00\00assertion failed: psize <= size + max_overhead\00\00`-\00\00)\00\00\00\b2\04\00\00\0d\00\00\00/Users/prytikov/.rustup/toolchains/1.88.0-aarch64-apple-darwin/lib/rustlib/src/rust/library/alloc/src/raw_vec/mod.rs\08.\00\00t\00\00\00.\02\00\00\11\00\00\00/Users/prytikov/Code/arbitrum-nitro/arbitrator/langs/rust/stylus-sdk/src/contract.rs\8c.\00\00T\00\00\00\19\00\00\00\15\00\00\00too many topics")
  (data (;1;) (i32.const 12032) "\02"))
