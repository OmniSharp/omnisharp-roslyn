#!/bin/bash

echo "Test"

function test() {
    para="${@:2}"

    for p in $para; do
        echo $p
    done
}

test 1 2 3 4 5 6

