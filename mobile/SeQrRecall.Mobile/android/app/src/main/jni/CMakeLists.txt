cmake_minimum_required(VERSION 3.13)

# Windows Ninja (MAX_PATH 260) fails when New Architecture codegen embeds the full
# source path in object filenames. Hash those names before they exceed the limit.
set(CMAKE_OBJECT_PATH_MAX 240)

project(appmodules)

include(${REACT_ANDROID_DIR}/cmake-utils/ReactNative-application.cmake)
