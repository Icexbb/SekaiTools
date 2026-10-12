# Emgu normally writes native libraries back into its source tree. Keep each
# experimental build's binaries and archives isolated instead.
function(sekai_isolate_outputs directory)
  get_property(targets DIRECTORY "${directory}" PROPERTY BUILDSYSTEM_TARGETS)
  foreach(target IN LISTS targets)
    get_target_property(kind "${target}" TYPE)
    if(kind MATCHES "^(STATIC_LIBRARY|SHARED_LIBRARY|MODULE_LIBRARY|EXECUTABLE)$")
      set_target_properties("${target}" PROPERTIES
        ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
        ARCHIVE_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/lib"
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/native"
        LIBRARY_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/native"
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/native"
        RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/native")
    endif()
  endforeach()
  get_property(children DIRECTORY "${directory}" PROPERTY SUBDIRECTORIES)
  foreach(child IN LISTS children)
    sekai_isolate_outputs("${child}")
  endforeach()
endfunction()
cmake_language(DEFER DIRECTORY "${CMAKE_SOURCE_DIR}" CALL sekai_isolate_outputs "${CMAKE_SOURCE_DIR}")

function(sekai_attach_diagnostics)
  if(DEFINED SEKAI_GRAY_CAPTURE AND NOT SEKAI_GRAY_CAPTURE STREQUAL "")
    target_sources(cvextern PRIVATE "${SEKAI_GRAY_CAPTURE}")
    target_include_directories(cvextern PRIVATE "${CMAKE_SOURCE_DIR}/opencv/modules/videoio/src")
  endif()
  if(DEFINED SEKAI_NATIVE_DIAGNOSTICS AND NOT SEKAI_NATIVE_DIAGNOSTICS STREQUAL "")
    target_sources(cvextern PRIVATE "${SEKAI_NATIVE_DIAGNOSTICS}")
  endif()
endfunction()
cmake_language(DEFER DIRECTORY "${CMAKE_SOURCE_DIR}" CALL sekai_attach_diagnostics)
