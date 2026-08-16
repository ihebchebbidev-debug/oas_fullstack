import * as React from "react"

import { cn } from "@/lib/utils"

const Input = React.forwardRef<HTMLInputElement, React.ComponentProps<"input">>(
  ({ className, type, ...props }, ref) => (
    <input
      type={type}
      className={cn(
        "flex h-8 w-full rounded-md border border-border/50 bg-input px-3 py-1.5 text-input-field shadow-sm ring-offset-background file:border-0 file:bg-transparent file:text-input-field file:font-medium file:text-foreground placeholder:text-muted-foreground/50 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring/70 focus-visible:border-ring/70 disabled:cursor-not-allowed disabled:opacity-50 transition-all duration-150 ease-out",
        className
      )}
      ref={ref}
      {...props}
    />
  )
)
Input.displayName = "Input"

export { Input }
