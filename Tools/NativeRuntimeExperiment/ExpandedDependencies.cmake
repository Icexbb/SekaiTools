# Emgu's bundled font configuration assumes lib/cmake package directories.
# vcpkg installs these configs under share and headers under include instead.
if(NOT DEFINED SEKAI_DEPENDENCY_PREFIX)
  message(FATAL_ERROR "Qualified dependency prefix required")
endif()
set(FREETYPE_INCLUDE_DIRS "${SEKAI_DEPENDENCY_PREFIX}/include")
set(HARFBUZZ_INCLUDE_DIRS "${SEKAI_DEPENDENCY_PREFIX}/include/harfbuzz")
if(NOT EXISTS "${FREETYPE_INCLUDE_DIRS}/ft2build.h" OR
   NOT EXISTS "${HARFBUZZ_INCLUDE_DIRS}/hb.h")
  message(FATAL_ERROR "Expanded font dependency headers missing")
endif()
