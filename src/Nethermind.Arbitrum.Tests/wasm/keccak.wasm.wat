(module
  (type (;0;) (func (result i32)))
  (type (;1;) (func (param i32)))
  (type (;2;) (func (param i32 i32 i32)))
  (type (;3;) (func (param i32 i32)))
  (type (;4;) (func))
  (type (;5;) (func (param i32) (result i32)))
  (type (;6;) (func (param i32 i32 i32) (result i32)))
  (import "vm_hooks" "msg_reentrant" (func (;0;) (type 0)))
  (import "vm_hooks" "read_args" (func (;1;) (type 1)))
  (import "vm_hooks" "native_keccak256" (func (;2;) (type 2)))
  (import "vm_hooks" "storage_flush_cache" (func (;3;) (type 1)))
  (import "vm_hooks" "write_result" (func (;4;) (type 3)))
  (import "vm_hooks" "pay_for_memory_grow" (func (;5;) (type 1)))
  (func (;6;) (type 2) (param i32 i32 i32)
    (local i32 i32 i32 i32)
    global.get 0
    i32.const 704
    i32.sub
    local.tee 3
    global.set 0
    local.get 3
    i32.const 0
    i32.const 200
    call 15
    local.tee 3
    i32.const 208
    i32.add
    i32.const 0
    i32.const 136
    call 15
    local.set 4
    local.get 3
    i32.const 0
    i32.store8 offset=344
    local.get 3
    i32.const 24
    i32.store offset=200
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          local.get 2
          i32.const 136
          i32.lt_u
          br_if 0 (;@3;)
          local.get 1
          local.get 2
          i32.const 136
          i32.div_u
          i32.const 136
          i32.mul
          local.tee 5
          i32.add
          local.set 6
          loop  ;; label = @4
            local.get 3
            local.get 3
            i64.load
            local.get 1
            i64.load align=1
            i64.xor
            i64.store
            local.get 3
            local.get 3
            i64.load offset=8
            local.get 1
            i32.const 8
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=8
            local.get 3
            local.get 3
            i64.load offset=16
            local.get 1
            i32.const 16
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=16
            local.get 3
            local.get 3
            i64.load offset=24
            local.get 1
            i32.const 24
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=24
            local.get 3
            local.get 3
            i64.load offset=32
            local.get 1
            i32.const 32
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=32
            local.get 3
            local.get 3
            i64.load offset=40
            local.get 1
            i32.const 40
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=40
            local.get 3
            local.get 3
            i64.load offset=48
            local.get 1
            i32.const 48
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=48
            local.get 3
            local.get 3
            i64.load offset=56
            local.get 1
            i32.const 56
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=56
            local.get 3
            local.get 3
            i64.load offset=64
            local.get 1
            i32.const 64
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=64
            local.get 3
            local.get 3
            i64.load offset=72
            local.get 1
            i32.const 72
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=72
            local.get 3
            local.get 3
            i64.load offset=80
            local.get 1
            i32.const 80
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=80
            local.get 3
            local.get 3
            i64.load offset=88
            local.get 1
            i32.const 88
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=88
            local.get 3
            local.get 3
            i64.load offset=96
            local.get 1
            i32.const 96
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=96
            local.get 3
            local.get 3
            i64.load offset=104
            local.get 1
            i32.const 104
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=104
            local.get 3
            local.get 3
            i64.load offset=112
            local.get 1
            i32.const 112
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=112
            local.get 3
            local.get 3
            i64.load offset=120
            local.get 1
            i32.const 120
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=120
            local.get 3
            local.get 3
            i64.load offset=128
            local.get 1
            i32.const 128
            i32.add
            i64.load align=1
            i64.xor
            i64.store offset=128
            local.get 3
            local.get 3
            i32.load offset=200
            call 7
            local.get 1
            i32.const 136
            i32.add
            local.tee 1
            local.get 6
            i32.ne
            br_if 0 (;@4;)
          end
          local.get 2
          local.get 5
          i32.sub
          local.tee 2
          i32.const 137
          i32.ge_u
          br_if 2 (;@1;)
          local.get 4
          local.get 6
          local.get 2
          call 17
          drop
          br 1 (;@2;)
        end
        local.get 4
        local.get 1
        local.get 2
        call 17
        drop
      end
      local.get 3
      local.get 2
      i32.store8 offset=344
      local.get 3
      i32.const 352
      i32.add
      local.get 3
      i32.const 352
      call 17
      drop
      local.get 3
      i32.const 560
      i32.add
      local.get 3
      i32.load8_u offset=696
      local.tee 1
      i32.add
      i32.const 0
      i32.const 136
      local.get 1
      i32.sub
      call 15
      i32.const 1
      i32.store8
      local.get 3
      i32.const 352
      i32.add
      i32.const 8
      i32.add
      local.tee 1
      local.get 1
      i64.load
      local.get 3
      i64.load offset=568
      i64.xor
      i64.store
      local.get 3
      i32.const 352
      i32.add
      i32.const 16
      i32.add
      local.tee 6
      local.get 6
      i64.load
      local.get 3
      i64.load offset=576
      i64.xor
      i64.store
      local.get 3
      i32.const 352
      i32.add
      i32.const 24
      i32.add
      local.tee 2
      local.get 2
      i64.load
      local.get 3
      i64.load offset=584
      i64.xor
      i64.store
      local.get 3
      local.get 3
      i32.load8_u offset=695
      i32.const 128
      i32.or
      i32.store8 offset=695
      local.get 3
      local.get 3
      i64.load offset=352
      local.get 3
      i64.load offset=560
      i64.xor
      i64.store offset=352
      local.get 3
      local.get 3
      i64.load offset=384
      local.get 3
      i64.load offset=592
      i64.xor
      i64.store offset=384
      local.get 3
      local.get 3
      i64.load offset=392
      local.get 3
      i64.load offset=600
      i64.xor
      i64.store offset=392
      local.get 3
      local.get 3
      i64.load offset=400
      local.get 3
      i64.load offset=608
      i64.xor
      i64.store offset=400
      local.get 3
      local.get 3
      i64.load offset=408
      local.get 3
      i64.load offset=616
      i64.xor
      i64.store offset=408
      local.get 3
      local.get 3
      i64.load offset=416
      local.get 3
      i64.load offset=624
      i64.xor
      i64.store offset=416
      local.get 3
      local.get 3
      i64.load offset=424
      local.get 3
      i64.load offset=632
      i64.xor
      i64.store offset=424
      local.get 3
      local.get 3
      i64.load offset=432
      local.get 3
      i64.load offset=640
      i64.xor
      i64.store offset=432
      local.get 3
      local.get 3
      i64.load offset=440
      local.get 3
      i64.load offset=648
      i64.xor
      i64.store offset=440
      local.get 3
      local.get 3
      i64.load offset=448
      local.get 3
      i64.load offset=656
      i64.xor
      i64.store offset=448
      local.get 3
      local.get 3
      i64.load offset=456
      local.get 3
      i64.load offset=664
      i64.xor
      i64.store offset=456
      local.get 3
      local.get 3
      i64.load offset=464
      local.get 3
      i64.load offset=672
      i64.xor
      i64.store offset=464
      local.get 3
      local.get 3
      i64.load offset=472
      local.get 3
      i64.load offset=680
      i64.xor
      i64.store offset=472
      local.get 3
      local.get 3
      i64.load offset=480
      local.get 3
      i64.load offset=688
      i64.xor
      i64.store offset=480
      local.get 3
      i32.const 352
      i32.add
      local.get 3
      i32.load offset=552
      call 7
      local.get 0
      i32.const 24
      i32.add
      local.get 2
      i64.load
      i64.store align=1
      local.get 0
      i32.const 16
      i32.add
      local.get 6
      i64.load
      i64.store align=1
      local.get 0
      i32.const 8
      i32.add
      local.get 1
      i64.load
      i64.store align=1
      local.get 0
      local.get 3
      i64.load offset=352
      i64.store align=1
      local.get 3
      i32.const 704
      i32.add
      global.set 0
      return
    end
    unreachable)
  (func (;7;) (type 3) (param i32 i32)
    (local i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64 i64)
    block  ;; label = @1
      local.get 1
      i32.const 24
      i32.gt_u
      br_if 0 (;@1;)
      block  ;; label = @2
        local.get 1
        i32.eqz
        br_if 0 (;@2;)
        i32.const 0
        local.get 1
        i32.const 3
        i32.shl
        i32.sub
        local.set 1
        local.get 0
        i64.load offset=192
        local.set 2
        local.get 0
        i64.load offset=152
        local.set 3
        local.get 0
        i64.load offset=112
        local.set 4
        local.get 0
        i64.load offset=72
        local.set 5
        local.get 0
        i64.load offset=32
        local.set 6
        local.get 0
        i64.load offset=184
        local.set 7
        local.get 0
        i64.load offset=144
        local.set 8
        local.get 0
        i64.load offset=104
        local.set 9
        local.get 0
        i64.load offset=64
        local.set 10
        local.get 0
        i64.load offset=24
        local.set 11
        local.get 0
        i64.load offset=176
        local.set 12
        local.get 0
        i64.load offset=136
        local.set 13
        local.get 0
        i64.load offset=96
        local.set 14
        local.get 0
        i64.load offset=56
        local.set 15
        local.get 0
        i64.load offset=16
        local.set 16
        local.get 0
        i64.load offset=168
        local.set 17
        local.get 0
        i64.load offset=128
        local.set 18
        local.get 0
        i64.load offset=88
        local.set 19
        local.get 0
        i64.load offset=48
        local.set 20
        local.get 0
        i64.load offset=8
        local.set 21
        local.get 0
        i64.load offset=160
        local.set 22
        local.get 0
        i64.load offset=120
        local.set 23
        local.get 0
        i64.load offset=80
        local.set 24
        local.get 0
        i64.load offset=40
        local.set 25
        local.get 0
        i64.load
        local.set 26
        loop  ;; label = @3
          local.get 12
          local.get 13
          local.get 14
          local.get 15
          local.get 16
          i64.xor
          i64.xor
          i64.xor
          i64.xor
          local.tee 27
          i64.const 1
          i64.rotl
          local.get 22
          local.get 23
          local.get 24
          local.get 25
          local.get 26
          i64.xor
          i64.xor
          i64.xor
          i64.xor
          local.tee 28
          i64.xor
          local.tee 29
          local.get 20
          i64.xor
          local.set 30
          local.get 2
          local.get 7
          local.get 8
          local.get 9
          local.get 10
          local.get 11
          i64.xor
          i64.xor
          i64.xor
          i64.xor
          local.tee 31
          local.get 28
          i64.const 1
          i64.rotl
          i64.xor
          local.tee 28
          i64.xor
          local.set 32
          local.get 2
          local.get 3
          local.get 4
          local.get 5
          local.get 6
          i64.xor
          i64.xor
          i64.xor
          i64.xor
          local.tee 33
          i64.const 1
          i64.rotl
          local.get 27
          i64.xor
          local.tee 27
          local.get 10
          i64.xor
          i64.const 55
          i64.rotl
          local.tee 34
          local.get 31
          i64.const 1
          i64.rotl
          local.get 17
          local.get 18
          local.get 19
          local.get 20
          local.get 21
          i64.xor
          i64.xor
          i64.xor
          i64.xor
          local.tee 10
          i64.xor
          local.tee 31
          local.get 16
          i64.xor
          i64.const 62
          i64.rotl
          local.tee 35
          i64.const -1
          i64.xor
          i64.and
          local.get 29
          local.get 17
          i64.xor
          i64.const 2
          i64.rotl
          local.tee 36
          i64.xor
          local.set 2
          local.get 33
          local.get 10
          i64.const 1
          i64.rotl
          i64.xor
          local.tee 16
          local.get 23
          i64.xor
          i64.const 41
          i64.rotl
          local.tee 33
          local.get 4
          local.get 28
          i64.xor
          i64.const 39
          i64.rotl
          local.tee 37
          i64.const -1
          i64.xor
          i64.and
          local.get 34
          i64.xor
          local.set 17
          local.get 27
          local.get 7
          i64.xor
          i64.const 56
          i64.rotl
          local.tee 38
          local.get 31
          local.get 13
          i64.xor
          i64.const 15
          i64.rotl
          local.tee 39
          i64.const -1
          i64.xor
          i64.and
          local.get 29
          local.get 19
          i64.xor
          i64.const 10
          i64.rotl
          local.tee 40
          i64.xor
          local.set 13
          local.get 40
          local.get 16
          local.get 25
          i64.xor
          i64.const 36
          i64.rotl
          local.tee 41
          i64.const -1
          i64.xor
          i64.and
          local.get 6
          local.get 28
          i64.xor
          i64.const 27
          i64.rotl
          local.tee 42
          i64.xor
          local.set 23
          local.get 16
          local.get 22
          i64.xor
          i64.const 18
          i64.rotl
          local.tee 22
          local.get 31
          local.get 15
          i64.xor
          i64.const 6
          i64.rotl
          local.tee 43
          local.get 29
          local.get 21
          i64.xor
          i64.const 1
          i64.rotl
          local.tee 44
          i64.const -1
          i64.xor
          i64.and
          i64.xor
          local.set 4
          local.get 3
          local.get 28
          i64.xor
          i64.const 8
          i64.rotl
          local.tee 45
          local.get 27
          local.get 9
          i64.xor
          i64.const 25
          i64.rotl
          local.tee 46
          i64.const -1
          i64.xor
          i64.and
          local.get 43
          i64.xor
          local.set 19
          local.get 5
          local.get 28
          i64.xor
          i64.const 20
          i64.rotl
          local.tee 28
          local.get 27
          local.get 11
          i64.xor
          i64.const 28
          i64.rotl
          local.tee 11
          i64.const -1
          i64.xor
          i64.and
          local.get 31
          local.get 12
          i64.xor
          i64.const 61
          i64.rotl
          local.tee 15
          i64.xor
          local.set 5
          local.get 11
          local.get 15
          i64.const -1
          i64.xor
          i64.and
          local.get 29
          local.get 18
          i64.xor
          i64.const 45
          i64.rotl
          local.tee 29
          i64.xor
          local.set 10
          local.get 16
          local.get 24
          i64.xor
          i64.const 3
          i64.rotl
          local.tee 21
          local.get 15
          local.get 29
          i64.const -1
          i64.xor
          i64.and
          i64.xor
          local.set 15
          local.get 29
          local.get 21
          i64.const -1
          i64.xor
          i64.and
          local.get 28
          i64.xor
          local.set 20
          local.get 21
          local.get 28
          i64.const -1
          i64.xor
          i64.and
          local.get 11
          i64.xor
          local.set 25
          local.get 27
          local.get 8
          i64.xor
          i64.const 21
          i64.rotl
          local.tee 29
          local.get 16
          local.get 26
          i64.xor
          local.tee 28
          local.get 32
          i64.const 14
          i64.rotl
          local.tee 27
          i64.const -1
          i64.xor
          i64.and
          i64.xor
          local.set 11
          local.get 27
          local.get 29
          i64.const -1
          i64.xor
          i64.and
          local.get 31
          local.get 14
          i64.xor
          i64.const 43
          i64.rotl
          local.tee 31
          i64.xor
          local.set 16
          local.get 29
          local.get 31
          i64.const -1
          i64.xor
          i64.and
          local.get 30
          i64.const 44
          i64.rotl
          local.tee 29
          i64.xor
          local.set 21
          local.get 31
          local.get 29
          i64.const -1
          i64.xor
          i64.and
          local.get 1
          i32.const 8384
          i32.add
          i64.load
          i64.xor
          local.get 28
          i64.xor
          local.set 26
          local.get 41
          local.get 42
          i64.const -1
          i64.xor
          i64.and
          local.get 38
          i64.xor
          local.tee 31
          local.set 3
          local.get 29
          local.get 28
          i64.const -1
          i64.xor
          i64.and
          local.get 27
          i64.xor
          local.tee 29
          local.set 6
          local.get 33
          local.get 35
          local.get 36
          i64.const -1
          i64.xor
          i64.and
          i64.xor
          local.tee 28
          local.set 7
          local.get 42
          local.get 38
          i64.const -1
          i64.xor
          i64.and
          local.get 39
          i64.xor
          local.tee 27
          local.set 8
          local.get 44
          local.get 22
          i64.const -1
          i64.xor
          i64.and
          local.get 45
          i64.xor
          local.tee 38
          local.set 9
          local.get 36
          local.get 33
          i64.const -1
          i64.xor
          i64.and
          local.get 37
          i64.xor
          local.tee 36
          local.set 12
          local.get 22
          local.get 45
          i64.const -1
          i64.xor
          i64.and
          local.get 46
          i64.xor
          local.tee 33
          local.set 14
          local.get 41
          local.get 39
          local.get 40
          i64.const -1
          i64.xor
          i64.and
          i64.xor
          local.tee 39
          local.set 18
          local.get 37
          local.get 34
          i64.const -1
          i64.xor
          i64.and
          local.get 35
          i64.xor
          local.tee 34
          local.set 22
          local.get 46
          local.get 43
          i64.const -1
          i64.xor
          i64.and
          local.get 44
          i64.xor
          local.tee 35
          local.set 24
          local.get 1
          i32.const 8
          i32.add
          local.tee 1
          br_if 0 (;@3;)
        end
        local.get 0
        local.get 34
        i64.store offset=160
        local.get 0
        local.get 23
        i64.store offset=120
        local.get 0
        local.get 35
        i64.store offset=80
        local.get 0
        local.get 25
        i64.store offset=40
        local.get 0
        local.get 17
        i64.store offset=168
        local.get 0
        local.get 39
        i64.store offset=128
        local.get 0
        local.get 19
        i64.store offset=88
        local.get 0
        local.get 20
        i64.store offset=48
        local.get 0
        local.get 21
        i64.store offset=8
        local.get 0
        local.get 36
        i64.store offset=176
        local.get 0
        local.get 13
        i64.store offset=136
        local.get 0
        local.get 33
        i64.store offset=96
        local.get 0
        local.get 15
        i64.store offset=56
        local.get 0
        local.get 16
        i64.store offset=16
        local.get 0
        local.get 28
        i64.store offset=184
        local.get 0
        local.get 27
        i64.store offset=144
        local.get 0
        local.get 38
        i64.store offset=104
        local.get 0
        local.get 10
        i64.store offset=64
        local.get 0
        local.get 11
        i64.store offset=24
        local.get 0
        local.get 2
        i64.store offset=192
        local.get 0
        local.get 31
        i64.store offset=152
        local.get 0
        local.get 4
        i64.store offset=112
        local.get 0
        local.get 5
        i64.store offset=72
        local.get 0
        local.get 29
        i64.store offset=32
        local.get 0
        local.get 26
        i64.store
      end
      return
    end
    unreachable)
  (func (;8;) (type 4)
    call 9
    unreachable)
  (func (;9;) (type 4)
    i32.const 0
    call 5)
  (func (;10;) (type 5) (param i32) (result i32)
    (local i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32)
    global.get 0
    i32.const 160
    i32.sub
    local.tee 1
    global.set 0
    block  ;; label = @1
      block  ;; label = @2
        block  ;; label = @3
          i32.const 0
          i32.load8_u offset=8384
          local.tee 2
          i32.const 2
          i32.ne
          br_if 0 (;@3;)
          i32.const 0
          call 0
          local.tee 2
          i32.store8 offset=8384
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
          local.get 0
          i32.const -1
          i32.le_s
          br_if 0 (;@3;)
          local.get 0
          i32.eqz
          br_if 1 (;@2;)
          i32.const 0
          i32.load8_u offset=8840
          drop
          local.get 0
          call 11
          local.tee 4
          i32.eqz
          br_if 0 (;@3;)
          local.get 4
          call 1
          local.get 1
          local.get 4
          i32.const 1
          i32.add
          local.get 0
          i32.const -1
          i32.add
          call 6
          block  ;; label = @4
            local.get 4
            i32.load8_u
            local.tee 5
            i32.const 2
            i32.lt_u
            br_if 0 (;@4;)
            i32.const 1
            local.set 6
            loop  ;; label = @5
              local.get 1
              i32.const 32
              i32.add
              local.get 1
              i32.const 32
              call 6
              local.get 1
              i32.const 96
              i32.add
              i32.const 24
              i32.add
              local.tee 7
              local.get 1
              i32.const 24
              i32.add
              local.tee 8
              i64.load align=1
              i64.store
              local.get 1
              i32.const 96
              i32.add
              i32.const 16
              i32.add
              local.tee 9
              local.get 1
              i32.const 16
              i32.add
              local.tee 10
              i64.load align=1
              i64.store
              local.get 1
              i32.const 96
              i32.add
              i32.const 8
              i32.add
              local.tee 11
              local.get 1
              i32.const 8
              i32.add
              local.tee 12
              i64.load align=1
              i64.store
              local.get 1
              local.get 1
              i64.load align=1
              i64.store offset=96
              local.get 1
              i32.const 128
              i32.add
              i32.const 24
              i32.add
              local.tee 2
              i64.const 0
              i64.store
              local.get 1
              i32.const 128
              i32.add
              i32.const 16
              i32.add
              local.tee 3
              i64.const 0
              i64.store
              local.get 1
              i32.const 128
              i32.add
              i32.const 8
              i32.add
              local.tee 13
              i64.const 0
              i64.store
              local.get 1
              i64.const 0
              i64.store offset=128
              local.get 1
              i32.const 96
              i32.add
              i32.const 32
              local.get 1
              i32.const 128
              i32.add
              call 2
              local.get 1
              i32.const 64
              i32.add
              i32.const 24
              i32.add
              local.tee 14
              local.get 2
              i64.load
              i64.store
              local.get 1
              i32.const 64
              i32.add
              i32.const 16
              i32.add
              local.tee 15
              local.get 3
              i64.load
              i64.store
              local.get 1
              i32.const 64
              i32.add
              i32.const 8
              i32.add
              local.tee 16
              local.get 13
              i64.load
              i64.store
              local.get 1
              local.get 1
              i64.load offset=128
              i64.store offset=64
              local.get 1
              i32.const 32
              i32.add
              local.get 1
              i32.const 64
              i32.add
              i32.const 32
              call 18
              br_if 2 (;@3;)
              local.get 7
              local.get 8
              i64.load align=1
              i64.store
              local.get 9
              local.get 10
              i64.load align=1
              i64.store
              local.get 11
              local.get 12
              i64.load align=1
              i64.store
              local.get 1
              local.get 1
              i64.load align=1
              i64.store offset=96
              local.get 2
              i64.const 0
              i64.store
              local.get 3
              i64.const 0
              i64.store
              local.get 13
              i64.const 0
              i64.store
              local.get 1
              i64.const 0
              i64.store offset=128
              local.get 1
              i32.const 96
              i32.add
              i32.const 32
              local.get 1
              i32.const 128
              i32.add
              call 2
              local.get 14
              local.get 2
              i64.load
              i64.store
              local.get 15
              local.get 3
              i64.load
              i64.store
              local.get 16
              local.get 13
              i64.load
              i64.store
              local.get 1
              local.get 1
              i64.load offset=128
              i64.store offset=64
              local.get 1
              i32.const 32
              i32.add
              local.get 1
              i32.const 64
              i32.add
              i32.const 32
              call 18
              br_if 2 (;@3;)
              local.get 12
              local.get 1
              i32.const 32
              i32.add
              i32.const 8
              i32.add
              i64.load align=1
              i64.store
              local.get 10
              local.get 1
              i32.const 32
              i32.add
              i32.const 16
              i32.add
              i64.load align=1
              i64.store
              local.get 8
              local.get 1
              i32.const 32
              i32.add
              i32.const 24
              i32.add
              i64.load align=1
              i64.store
              local.get 1
              local.get 1
              i64.load offset=32 align=1
              i64.store
              local.get 6
              i32.const 1
              i32.add
              local.tee 6
              i32.const 255
              i32.and
              local.get 5
              i32.lt_u
              br_if 0 (;@5;)
            end
          end
          i32.const 0
          local.set 3
          i32.const 0
          i32.load8_u offset=8840
          drop
          i32.const 32
          call 11
          local.tee 2
          i32.eqz
          br_if 0 (;@3;)
          local.get 2
          local.get 1
          i64.load
          i64.store align=1
          local.get 2
          i32.const 24
          i32.add
          local.get 1
          i32.const 24
          i32.add
          i64.load
          i64.store align=1
          local.get 2
          i32.const 16
          i32.add
          local.get 1
          i32.const 16
          i32.add
          i64.load
          i64.store align=1
          local.get 2
          i32.const 8
          i32.add
          local.get 1
          i32.const 8
          i32.add
          i64.load
          i64.store align=1
          local.get 4
          local.get 0
          call 12
          i32.const 0
          call 3
          local.get 2
          i32.const 32
          call 4
          local.get 2
          i32.const 32
          call 12
          br 2 (;@1;)
        end
        unreachable
      end
      i32.const 1
      call 1
      unreachable
    end
    local.get 1
    i32.const 160
    i32.add
    global.set 0
    local.get 3)
  (func (;11;) (type 5) (param i32) (result i32)
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
                    i32.load offset=8800
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
                      i32.const 8388
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
                    i32.load offset=8796
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
                        i32.const 8532
                        i32.add
                        local.tee 2
                        local.get 0
                        i32.const 8540
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
                      i32.store offset=8796
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
                  i32.load offset=8804
                  i32.le_u
                  br_if 3 (;@4;)
                  block  ;; label = @8
                    block  ;; label = @9
                      block  ;; label = @10
                        local.get 0
                        br_if 0 (;@10;)
                        i32.const 0
                        i32.load offset=8800
                        local.tee 0
                        i32.eqz
                        br_if 6 (;@4;)
                        local.get 0
                        i32.ctz
                        i32.const 2
                        i32.shl
                        i32.const 8388
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
                                i32.const 8388
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
                          i32.const 8532
                          i32.add
                          local.tee 6
                          local.get 1
                          i32.const 8540
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
                        i32.store offset=8796
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
                        i32.load offset=8804
                        local.tee 5
                        i32.eqz
                        br_if 0 (;@10;)
                        local.get 5
                        i32.const -8
                        i32.and
                        i32.const 8532
                        i32.add
                        local.set 6
                        i32.const 0
                        i32.load offset=8812
                        local.set 1
                        block  ;; label = @11
                          block  ;; label = @12
                            i32.const 0
                            i32.load offset=8796
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
                            i32.store offset=8796
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
                      i32.store offset=8812
                      i32.const 0
                      local.get 2
                      i32.store offset=8804
                      local.get 0
                      i32.const 8
                      i32.add
                      return
                    end
                    i32.const 0
                    i32.const 0
                    i32.load offset=8800
                    i32.const -2
                    local.get 5
                    i32.load offset=28
                    i32.rotl
                    i32.and
                    i32.store offset=8800
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
                        i32.load offset=8804
                        local.tee 7
                        i32.eqz
                        br_if 1 (;@9;)
                        local.get 7
                        i32.const -8
                        i32.and
                        i32.const 8532
                        i32.add
                        local.set 6
                        i32.const 0
                        i32.load offset=8812
                        local.set 0
                        block  ;; label = @11
                          block  ;; label = @12
                            i32.const 0
                            i32.load offset=8796
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
                            i32.store offset=8796
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
                    i32.store offset=8812
                    i32.const 0
                    local.get 1
                    i32.store offset=8804
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
                  i32.const 8388
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
              i32.load offset=8804
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
                i32.const 8388
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
                      i32.load offset=8804
                      local.tee 0
                      local.get 2
                      i32.ge_u
                      br_if 0 (;@9;)
                      block  ;; label = @10
                        i32.const 0
                        i32.load offset=8808
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
                        i32.load offset=8820
                        i32.const 0
                        local.get 1
                        i32.const -65536
                        i32.and
                        local.get 7
                        select
                        local.tee 8
                        i32.add
                        local.tee 0
                        i32.store offset=8820
                        i32.const 0
                        local.get 0
                        i32.const 0
                        i32.load offset=8824
                        local.tee 1
                        local.get 0
                        local.get 1
                        i32.gt_u
                        select
                        i32.store offset=8824
                        block  ;; label = @11
                          block  ;; label = @12
                            block  ;; label = @13
                              i32.const 0
                              i32.load offset=8816
                              local.tee 1
                              i32.eqz
                              br_if 0 (;@13;)
                              i32.const 8516
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
                                i32.load offset=8832
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
                              i32.store offset=8832
                            end
                            i32.const 0
                            i32.const 4095
                            i32.store offset=8836
                            i32.const 0
                            local.get 8
                            i32.store offset=8520
                            i32.const 0
                            local.get 5
                            i32.store offset=8516
                            i32.const 0
                            i32.const 8532
                            i32.store offset=8544
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
                            i32.const 0
                            i32.store offset=8528
                            i32.const 0
                            i32.const 8596
                            i32.store offset=8608
                            i32.const 0
                            i32.const 8588
                            i32.store offset=8596
                            i32.const 0
                            i32.const 8596
                            i32.store offset=8604
                            i32.const 0
                            i32.const 8604
                            i32.store offset=8616
                            i32.const 0
                            i32.const 8604
                            i32.store offset=8612
                            i32.const 0
                            i32.const 8612
                            i32.store offset=8624
                            i32.const 0
                            i32.const 8612
                            i32.store offset=8620
                            i32.const 0
                            i32.const 8620
                            i32.store offset=8632
                            i32.const 0
                            i32.const 8620
                            i32.store offset=8628
                            i32.const 0
                            i32.const 8628
                            i32.store offset=8640
                            i32.const 0
                            i32.const 8628
                            i32.store offset=8636
                            i32.const 0
                            i32.const 8636
                            i32.store offset=8648
                            i32.const 0
                            i32.const 8636
                            i32.store offset=8644
                            i32.const 0
                            i32.const 8644
                            i32.store offset=8656
                            i32.const 0
                            i32.const 8644
                            i32.store offset=8652
                            i32.const 0
                            i32.const 8652
                            i32.store offset=8664
                            i32.const 0
                            i32.const 8652
                            i32.store offset=8660
                            i32.const 0
                            i32.const 8660
                            i32.store offset=8672
                            i32.const 0
                            i32.const 8668
                            i32.store offset=8680
                            i32.const 0
                            i32.const 8660
                            i32.store offset=8668
                            i32.const 0
                            i32.const 8676
                            i32.store offset=8688
                            i32.const 0
                            i32.const 8668
                            i32.store offset=8676
                            i32.const 0
                            i32.const 8684
                            i32.store offset=8696
                            i32.const 0
                            i32.const 8676
                            i32.store offset=8684
                            i32.const 0
                            i32.const 8692
                            i32.store offset=8704
                            i32.const 0
                            i32.const 8684
                            i32.store offset=8692
                            i32.const 0
                            i32.const 8700
                            i32.store offset=8712
                            i32.const 0
                            i32.const 8692
                            i32.store offset=8700
                            i32.const 0
                            i32.const 8708
                            i32.store offset=8720
                            i32.const 0
                            i32.const 8700
                            i32.store offset=8708
                            i32.const 0
                            i32.const 8716
                            i32.store offset=8728
                            i32.const 0
                            i32.const 8708
                            i32.store offset=8716
                            i32.const 0
                            i32.const 8724
                            i32.store offset=8736
                            i32.const 0
                            i32.const 8716
                            i32.store offset=8724
                            i32.const 0
                            i32.const 8732
                            i32.store offset=8744
                            i32.const 0
                            i32.const 8724
                            i32.store offset=8732
                            i32.const 0
                            i32.const 8740
                            i32.store offset=8752
                            i32.const 0
                            i32.const 8732
                            i32.store offset=8740
                            i32.const 0
                            i32.const 8748
                            i32.store offset=8760
                            i32.const 0
                            i32.const 8740
                            i32.store offset=8748
                            i32.const 0
                            i32.const 8756
                            i32.store offset=8768
                            i32.const 0
                            i32.const 8748
                            i32.store offset=8756
                            i32.const 0
                            i32.const 8764
                            i32.store offset=8776
                            i32.const 0
                            i32.const 8756
                            i32.store offset=8764
                            i32.const 0
                            i32.const 8772
                            i32.store offset=8784
                            i32.const 0
                            i32.const 8764
                            i32.store offset=8772
                            i32.const 0
                            i32.const 8780
                            i32.store offset=8792
                            i32.const 0
                            i32.const 8772
                            i32.store offset=8780
                            i32.const 0
                            local.get 5
                            i32.store offset=8816
                            i32.const 0
                            i32.const 8780
                            i32.store offset=8788
                            i32.const 0
                            local.get 8
                            i32.const -40
                            i32.add
                            local.tee 0
                            i32.store offset=8808
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
                            i32.store offset=8828
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
                        i32.load offset=8832
                        local.tee 0
                        local.get 5
                        local.get 0
                        local.get 5
                        i32.lt_u
                        select
                        i32.store offset=8832
                        local.get 5
                        local.get 8
                        i32.add
                        local.set 6
                        i32.const 8516
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
                          i32.const 8516
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
                          i32.store offset=8816
                          i32.const 0
                          local.get 8
                          i32.const -40
                          i32.add
                          local.tee 0
                          i32.store offset=8808
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
                          i32.store offset=8828
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
                          i64.load offset=8516 align=4
                          local.set 9
                          local.get 7
                          i32.const 16
                          i32.add
                          i32.const 0
                          i64.load offset=8524 align=4
                          i64.store align=4
                          local.get 7
                          local.get 9
                          i64.store offset=8 align=4
                          i32.const 0
                          local.get 8
                          i32.store offset=8520
                          i32.const 0
                          local.get 5
                          i32.store offset=8516
                          i32.const 0
                          local.get 7
                          i32.const 8
                          i32.add
                          i32.store offset=8524
                          i32.const 0
                          i32.const 0
                          i32.store offset=8528
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
                            call 13
                            br 8 (;@4;)
                          end
                          local.get 0
                          i32.const 248
                          i32.and
                          i32.const 8532
                          i32.add
                          local.set 6
                          block  ;; label = @12
                            block  ;; label = @13
                              i32.const 0
                              i32.load offset=8796
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
                              i32.store offset=8796
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
                        i32.load offset=8816
                        i32.eq
                        br_if 3 (;@7;)
                        local.get 6
                        i32.const 0
                        i32.load offset=8812
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
                          call 14
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
                          call 13
                          br 6 (;@5;)
                        end
                        local.get 1
                        i32.const 248
                        i32.and
                        i32.const 8532
                        i32.add
                        local.set 2
                        block  ;; label = @11
                          block  ;; label = @12
                            i32.const 0
                            i32.load offset=8796
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
                            i32.store offset=8796
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
                      i32.store offset=8808
                      i32.const 0
                      i32.const 0
                      i32.load offset=8816
                      local.tee 0
                      local.get 2
                      i32.add
                      local.tee 6
                      i32.store offset=8816
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
                    i32.load offset=8812
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
                        i32.store offset=8812
                        i32.const 0
                        i32.const 0
                        i32.store offset=8804
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
                      i32.store offset=8804
                      i32.const 0
                      local.get 1
                      local.get 2
                      i32.add
                      local.tee 5
                      i32.store offset=8812
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
                  i32.load offset=8816
                  local.tee 0
                  i32.const 15
                  i32.add
                  i32.const -8
                  i32.and
                  local.tee 1
                  i32.const -8
                  i32.add
                  local.tee 6
                  i32.store offset=8816
                  i32.const 0
                  local.get 0
                  local.get 1
                  i32.sub
                  i32.const 0
                  i32.load offset=8808
                  local.get 8
                  i32.add
                  local.tee 1
                  i32.add
                  i32.const 8
                  i32.add
                  local.tee 5
                  i32.store offset=8808
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
                  i32.store offset=8828
                  br 3 (;@4;)
                end
                i32.const 0
                local.get 0
                i32.store offset=8816
                i32.const 0
                i32.const 0
                i32.load offset=8808
                local.get 1
                i32.add
                local.tee 1
                i32.store offset=8808
                local.get 0
                local.get 1
                i32.const 1
                i32.or
                i32.store offset=4
                br 1 (;@5;)
              end
              i32.const 0
              local.get 0
              i32.store offset=8812
              i32.const 0
              i32.const 0
              i32.load offset=8804
              local.get 1
              i32.add
              local.tee 1
              i32.store offset=8804
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
          i32.load offset=8808
          local.tee 0
          local.get 2
          i32.le_u
          br_if 0 (;@3;)
          i32.const 0
          local.get 0
          local.get 2
          i32.sub
          local.tee 1
          i32.store offset=8808
          i32.const 0
          i32.const 0
          i32.load offset=8816
          local.tee 0
          local.get 2
          i32.add
          local.tee 6
          i32.store offset=8816
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
      i32.load offset=8800
      i32.const -2
      local.get 6
      i32.load offset=28
      i32.rotl
      i32.and
      i32.store offset=8800
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
          i32.const 8388
          i32.add
          local.set 5
          block  ;; label = @4
            i32.const 0
            i32.load offset=8800
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
            i32.load offset=8800
            local.get 7
            i32.or
            i32.store offset=8800
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
        i32.const 8532
        i32.add
        local.set 0
        block  ;; label = @3
          block  ;; label = @4
            i32.const 0
            i32.load offset=8796
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
            i32.store offset=8796
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
  (func (;12;) (type 3) (param i32 i32)
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
                    i32.load offset=8812
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
                    i32.store offset=8804
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
                  call 14
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
                        i32.load offset=8816
                        i32.eq
                        br_if 2 (;@8;)
                        local.get 1
                        i32.const 0
                        i32.load offset=8812
                        i32.eq
                        br_if 3 (;@7;)
                        local.get 1
                        local.get 2
                        i32.const -8
                        i32.and
                        local.tee 2
                        call 14
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
                        i32.load offset=8812
                        i32.ne
                        br_if 1 (;@9;)
                        i32.const 0
                        local.get 3
                        i32.store offset=8804
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
                    i32.const 8388
                    i32.add
                    local.set 1
                    i32.const 0
                    i32.load offset=8800
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
                    i32.load offset=8800
                    local.get 4
                    i32.or
                    i32.store offset=8800
                    br 5 (;@3;)
                  end
                  i32.const 0
                  local.get 0
                  i32.store offset=8816
                  i32.const 0
                  i32.const 0
                  i32.load offset=8808
                  local.get 3
                  i32.add
                  local.tee 3
                  i32.store offset=8808
                  local.get 0
                  local.get 3
                  i32.const 1
                  i32.or
                  i32.store offset=4
                  block  ;; label = @8
                    local.get 0
                    i32.const 0
                    i32.load offset=8812
                    i32.ne
                    br_if 0 (;@8;)
                    i32.const 0
                    i32.const 0
                    i32.store offset=8804
                    i32.const 0
                    i32.const 0
                    i32.store offset=8812
                  end
                  local.get 3
                  i32.const 0
                  i32.load offset=8828
                  local.tee 4
                  i32.le_u
                  br_if 6 (;@1;)
                  i32.const 0
                  i32.load offset=8816
                  local.tee 0
                  i32.eqz
                  br_if 6 (;@1;)
                  i32.const 0
                  local.set 1
                  i32.const 0
                  i32.load offset=8808
                  local.tee 5
                  i32.const 41
                  i32.lt_u
                  br_if 5 (;@2;)
                  i32.const 8516
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
                i32.store offset=8812
                i32.const 0
                i32.const 0
                i32.load offset=8804
                local.get 3
                i32.add
                local.tee 3
                i32.store offset=8804
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
            i32.const 8532
            i32.add
            local.set 2
            block  ;; label = @5
              block  ;; label = @6
                i32.const 0
                i32.load offset=8796
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
                i32.store offset=8796
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
        i32.load offset=8836
        i32.const -1
        i32.add
        local.tee 0
        i32.store offset=8836
        local.get 0
        br_if 1 (;@1;)
        block  ;; label = @3
          i32.const 0
          i32.load offset=8524
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
        i32.store offset=8836
        return
      end
      block  ;; label = @2
        i32.const 0
        i32.load offset=8524
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
      i32.store offset=8836
      local.get 5
      local.get 4
      i32.le_u
      br_if 0 (;@1;)
      i32.const 0
      i32.const -1
      i32.store offset=8828
      return
    end)
  (func (;13;) (type 3) (param i32 i32)
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
    i32.const 8388
    i32.add
    local.set 3
    block  ;; label = @1
      i32.const 0
      i32.load offset=8800
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
      i32.load offset=8800
      local.get 4
      i32.or
      i32.store offset=8800
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
  (func (;14;) (type 3) (param i32 i32)
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
                i32.const 8388
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
          i32.load offset=8796
          i32.const -2
          local.get 1
          i32.const 3
          i32.shr_u
          i32.rotl
          i32.and
          i32.store offset=8796
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
    i32.load offset=8800
    i32.const -2
    local.get 0
    i32.load offset=28
    i32.rotl
    i32.and
    i32.store offset=8800)
  (func (;15;) (type 6) (param i32 i32 i32) (result i32)
    (local i32 i32 i32 i32)
    block  ;; label = @1
      block  ;; label = @2
        local.get 2
        i32.const 16
        i32.ge_u
        br_if 0 (;@2;)
        local.get 0
        local.set 3
        br 1 (;@1;)
      end
      block  ;; label = @2
        local.get 0
        local.get 0
        i32.const 0
        local.get 0
        i32.sub
        i32.const 3
        i32.and
        local.tee 4
        i32.add
        local.tee 5
        i32.ge_u
        br_if 0 (;@2;)
        local.get 4
        local.set 6
        local.get 0
        local.set 3
        loop  ;; label = @3
          local.get 3
          local.get 1
          i32.store8
          local.get 3
          i32.const 1
          i32.add
          local.set 3
          local.get 6
          i32.const -1
          i32.add
          local.tee 6
          br_if 0 (;@3;)
        end
      end
      block  ;; label = @2
        local.get 5
        local.get 5
        local.get 2
        local.get 4
        i32.sub
        local.tee 6
        i32.const -4
        i32.and
        i32.add
        local.tee 3
        i32.ge_u
        br_if 0 (;@2;)
        local.get 1
        i32.const 255
        i32.and
        i32.const 16843009
        i32.mul
        local.set 2
        loop  ;; label = @3
          local.get 5
          local.get 2
          i32.store
          local.get 5
          i32.const 4
          i32.add
          local.tee 5
          local.get 3
          i32.lt_u
          br_if 0 (;@3;)
        end
      end
      local.get 6
      i32.const 3
      i32.and
      local.set 2
    end
    block  ;; label = @1
      local.get 3
      local.get 3
      local.get 2
      i32.add
      i32.ge_u
      br_if 0 (;@1;)
      loop  ;; label = @2
        local.get 3
        local.get 1
        i32.store8
        local.get 3
        i32.const 1
        i32.add
        local.set 3
        local.get 2
        i32.const -1
        i32.add
        local.tee 2
        br_if 0 (;@2;)
      end
    end
    local.get 0)
  (func (;16;) (type 6) (param i32 i32 i32) (result i32)
    (local i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32 i32)
    global.get 0
    i32.const 16
    i32.sub
    local.set 3
    block  ;; label = @1
      block  ;; label = @2
        local.get 2
        i32.const 16
        i32.ge_u
        br_if 0 (;@2;)
        local.get 0
        local.set 4
        br 1 (;@1;)
      end
      block  ;; label = @2
        local.get 0
        local.get 0
        i32.const 0
        local.get 0
        i32.sub
        i32.const 3
        i32.and
        local.tee 5
        i32.add
        local.tee 6
        i32.ge_u
        br_if 0 (;@2;)
        local.get 5
        local.set 7
        local.get 0
        local.set 4
        local.get 1
        local.set 8
        loop  ;; label = @3
          local.get 4
          local.get 8
          i32.load8_u
          i32.store8
          local.get 8
          i32.const 1
          i32.add
          local.set 8
          local.get 4
          i32.const 1
          i32.add
          local.set 4
          local.get 7
          i32.const -1
          i32.add
          local.tee 7
          br_if 0 (;@3;)
        end
      end
      local.get 6
      local.get 2
      local.get 5
      i32.sub
      local.tee 2
      i32.const -4
      i32.and
      local.tee 7
      i32.add
      local.set 4
      block  ;; label = @2
        block  ;; label = @3
          local.get 1
          local.get 5
          i32.add
          local.tee 8
          i32.const 3
          i32.and
          local.tee 1
          br_if 0 (;@3;)
          local.get 6
          local.get 4
          i32.ge_u
          br_if 1 (;@2;)
          local.get 8
          local.set 1
          loop  ;; label = @4
            local.get 6
            local.get 1
            i32.load
            i32.store
            local.get 1
            i32.const 4
            i32.add
            local.set 1
            local.get 6
            i32.const 4
            i32.add
            local.tee 6
            local.get 4
            i32.lt_u
            br_if 0 (;@4;)
            br 2 (;@2;)
          end
        end
        i32.const 0
        local.set 5
        local.get 3
        i32.const 0
        i32.store offset=12
        local.get 3
        i32.const 12
        i32.add
        local.get 1
        i32.or
        local.set 9
        block  ;; label = @3
          i32.const 4
          local.get 1
          i32.sub
          local.tee 10
          i32.const 1
          i32.and
          i32.eqz
          br_if 0 (;@3;)
          local.get 9
          local.get 8
          i32.load8_u
          i32.store8
          i32.const 1
          local.set 5
        end
        block  ;; label = @3
          local.get 10
          i32.const 2
          i32.and
          i32.eqz
          br_if 0 (;@3;)
          local.get 9
          local.get 5
          i32.add
          local.get 8
          local.get 5
          i32.add
          i32.load16_u
          i32.store16
        end
        local.get 8
        local.get 1
        i32.sub
        local.set 5
        local.get 1
        i32.const 3
        i32.shl
        local.set 11
        local.get 3
        i32.load offset=12
        local.set 9
        block  ;; label = @3
          block  ;; label = @4
            local.get 6
            i32.const 4
            i32.add
            local.get 4
            i32.lt_u
            br_if 0 (;@4;)
            local.get 6
            local.set 12
            br 1 (;@3;)
          end
          i32.const 0
          local.get 11
          i32.sub
          i32.const 24
          i32.and
          local.set 13
          loop  ;; label = @4
            local.get 6
            local.get 9
            local.get 11
            i32.shr_u
            local.get 5
            i32.const 4
            i32.add
            local.tee 5
            i32.load
            local.tee 9
            local.get 13
            i32.shl
            i32.or
            i32.store
            local.get 6
            i32.const 8
            i32.add
            local.set 10
            local.get 6
            i32.const 4
            i32.add
            local.tee 12
            local.set 6
            local.get 10
            local.get 4
            i32.lt_u
            br_if 0 (;@4;)
          end
        end
        i32.const 0
        local.set 6
        local.get 3
        i32.const 0
        i32.store8 offset=8
        local.get 3
        i32.const 0
        i32.store8 offset=6
        block  ;; label = @3
          block  ;; label = @4
            local.get 1
            i32.const 1
            i32.ne
            br_if 0 (;@4;)
            local.get 3
            i32.const 8
            i32.add
            local.set 13
            i32.const 0
            local.set 1
            i32.const 0
            local.set 10
            i32.const 0
            local.set 14
            br 1 (;@3;)
          end
          local.get 5
          i32.const 5
          i32.add
          i32.load8_u
          local.set 10
          local.get 3
          local.get 5
          i32.const 4
          i32.add
          i32.load8_u
          local.tee 1
          i32.store8 offset=8
          local.get 10
          i32.const 8
          i32.shl
          local.set 10
          i32.const 2
          local.set 14
          local.get 3
          i32.const 6
          i32.add
          local.set 13
        end
        block  ;; label = @3
          local.get 8
          i32.const 1
          i32.and
          i32.eqz
          br_if 0 (;@3;)
          local.get 13
          local.get 5
          i32.const 4
          i32.add
          local.get 14
          i32.add
          i32.load8_u
          i32.store8
          local.get 3
          i32.load8_u offset=6
          i32.const 16
          i32.shl
          local.set 6
          local.get 3
          i32.load8_u offset=8
          local.set 1
        end
        local.get 12
        local.get 10
        local.get 6
        i32.or
        local.get 1
        i32.const 255
        i32.and
        i32.or
        i32.const 0
        local.get 11
        i32.sub
        i32.const 24
        i32.and
        i32.shl
        local.get 9
        local.get 11
        i32.shr_u
        i32.or
        i32.store
      end
      local.get 2
      i32.const 3
      i32.and
      local.set 2
      local.get 8
      local.get 7
      i32.add
      local.set 1
    end
    block  ;; label = @1
      local.get 4
      local.get 4
      local.get 2
      i32.add
      i32.ge_u
      br_if 0 (;@1;)
      loop  ;; label = @2
        local.get 4
        local.get 1
        i32.load8_u
        i32.store8
        local.get 1
        i32.const 1
        i32.add
        local.set 1
        local.get 4
        i32.const 1
        i32.add
        local.set 4
        local.get 2
        i32.const -1
        i32.add
        local.tee 2
        br_if 0 (;@2;)
      end
    end
    local.get 0)
  (func (;17;) (type 6) (param i32 i32 i32) (result i32)
    local.get 0
    local.get 1
    local.get 2
    call 16)
  (func (;18;) (type 6) (param i32 i32 i32) (result i32)
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
  (table (;0;) 1 1 funcref)
  (memory (;0;) 1)
  (global (;0;) (mut i32) (i32.const 8192))
  (global (;1;) i32 (i32.const 8841))
  (global (;2;) i32 (i32.const 8848))
  (export "memory" (memory 0))
  (export "mark_used" (func 8))
  (export "user_entrypoint" (func 10))
  (export "__data_end" (global 1))
  (export "__heap_base" (global 2))
  (data (;0;) (i32.const 8192) "\01\00\00\00\00\00\00\00\82\80\00\00\00\00\00\00\8a\80\00\00\00\00\00\80\00\80\00\80\00\00\00\80\8b\80\00\00\00\00\00\00\01\00\00\80\00\00\00\00\81\80\00\80\00\00\00\80\09\80\00\00\00\00\00\80\8a\00\00\00\00\00\00\00\88\00\00\00\00\00\00\00\09\80\00\80\00\00\00\00\0a\00\00\80\00\00\00\00\8b\80\00\80\00\00\00\00\8b\00\00\00\00\00\00\80\89\80\00\00\00\00\00\80\03\80\00\00\00\00\00\80\02\80\00\00\00\00\00\80\80\00\00\00\00\00\00\80\0a\80\00\00\00\00\00\00\0a\00\00\80\00\00\00\80\81\80\00\80\00\00\00\80\80\80\00\00\00\00\00\80\01\00\00\80\00\00\00\00\08\80\00\80\00\00\00\80")
  (data (;1;) (i32.const 8384) "\02"))
