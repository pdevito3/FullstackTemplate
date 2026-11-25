import type * as React from "react"

/** Whether a prop that takes text or something else, such as a node or a render function, holds text. */
export function isText<T>(value: T): value is Extract<T, string> {
  return typeof value === "string"
}

/** Whether a ref is a callback ref, not an object ref. */
export function isRefCallback<T>(ref: React.Ref<T> | undefined): ref is React.RefCallback<T> {
  return typeof ref === "function"
}

/** Whether a state setter got a function of the previous value, not the new value. */
export function isUpdater<T>(value: T | ((previous: T) => T)): value is (previous: T) => T {
  return typeof value === "function"
}
