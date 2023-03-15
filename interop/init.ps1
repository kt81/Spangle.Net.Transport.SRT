git submodule update --init

cd vcpkg
./bootstrap-vcpkg.bat

./vcpkg install openssl --triplet x64-windows-md
./vcpkg integrate install
cd ..