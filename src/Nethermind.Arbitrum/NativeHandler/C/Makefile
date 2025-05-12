LIB_NAME = libstylus.so
SRC_DIR = interop
TARGET_DIR = /Users/tanishqjasoria/GolandProjects/nitro/target
INCLUDE_DIR = $(TARGET_DIR)/include
STATIC_LIB = $(TARGET_DIR)/lib/libstylus.a

$(LIB_NAME): $(SRC_DIR)/wrapper.c
	@gcc -fPIC -shared $(SRC_DIR)/wrapper.c -I$(INCLUDE_DIR) $(STATIC_LIB) -ldl -lm -o $(LIB_NAME)

clean:
	@rm -f $(LIB_NAME)