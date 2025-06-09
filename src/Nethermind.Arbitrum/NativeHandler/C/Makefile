LIB_NAME = libstylus
SRC_DIR = interop
NITRO_TARGET_DIR ?= /Users/tanishqjasoria/GolandProjects/nitro/target
INCLUDE_DIR = $(NITRO_TARGET_DIR)/include
STATIC_LIB = $(NITRO_TARGET_DIR)/lib/libstylus.a

CFLAGS ?= -fPIC
LDFLAGS ?= -shared
LDLIBS ?= -ldl -lm

.PHONY: all clean

all: $(LIB_NAME)


$(LIB_NAME): $(SRC_DIR)/wrapper.c $(STATIC_LIB)
	@echo "--- Checking Prerequisites ---"
	@test -f "$(SRC_DIR)/wrapper.c" || (echo "Error: $(SRC_DIR)/wrapper.c not found" && exit 1)
	@test -d "$(INCLUDE_DIR)" || (echo "Error: Include directory $(INCLUDE_DIR) not found" && exit 1)
	@test -f "$(STATIC_LIB)" || (echo "Error: Static library $(STATIC_LIB) not found" && exit 1)
	
	@echo "--- Building Shared Library: $@ ---"
	@gcc $(CFLAGS) $(LDFLAGS) $(SRC_DIR)/wrapper.c -I$(INCLUDE_DIR) $(STATIC_LIB) $(LDLIBS) -o $(LIB_NAME)
	@echo "--- Build Complete: $(LIB_NAME) created successfully ---"

clean:
	@echo "--- Cleaning up built files ---"
	@rm -f $(LIB_NAME)