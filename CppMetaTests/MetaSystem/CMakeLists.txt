add_library(Meta "")

target_sources(Meta
  PRIVATE  
    ${CMAKE_CURRENT_LIST_DIR}/Attribute.cpp
    ${CMAKE_CURRENT_LIST_DIR}/Function.cpp
    ${CMAKE_CURRENT_LIST_DIR}/Type.cpp
    ${CMAKE_CURRENT_LIST_DIR}/Attribute.hpp
    ${CMAKE_CURRENT_LIST_DIR}/Field.hpp
    ${CMAKE_CURRENT_LIST_DIR}/ForwardDeclarations.hpp
    ${CMAKE_CURRENT_LIST_DIR}/Function.hpp
    ${CMAKE_CURRENT_LIST_DIR}/Property.hpp
    ${CMAKE_CURRENT_LIST_DIR}/Reflection.hpp
    ${CMAKE_CURRENT_LIST_DIR}/Meta.hpp
    ${CMAKE_CURRENT_LIST_DIR}/Type.hpp
)
